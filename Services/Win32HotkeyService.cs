using System.Diagnostics;
using System.Runtime.InteropServices;
using SwitchCast.Models;

namespace SwitchCast.Services;

/// <summary>
/// Native Win32 implementation of IHotkeyService using RegisterHotKey and a lightweight message window.
/// </summary>
public sealed class Win32HotkeyService : IHotkeyService
{
    private const uint WM_HOTKEY = 0x0312;
    private static readonly IntPtr HWND_MESSAGE = new(-3);

    private readonly IApplicationSettingsService _settingsService;
    private readonly Dictionary<int, HotkeyBinding> _registeredBindings = new();
    private readonly object _lock = new();

    private IntPtr _hwnd = IntPtr.Zero;
    private WndProcDelegate? _wndProcDelegate;
    private string _windowClassName = string.Empty;
    private bool _isDisposed;
    private bool _isEnabled = true;
    private int _nextId = 1;

    public Win32HotkeyService(IApplicationSettingsService settingsService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _isEnabled = _settingsService.CurrentSettings.EnableGlobalHotkeys;
    }

    public bool IsEnabled => _isEnabled;

    public IReadOnlyList<HotkeyBinding> CurrentBindings
    {
        get
        {
            lock (_lock)
            {
                return _settingsService.CurrentSettings.HotkeyBindings.AsReadOnly();
            }
        }
    }

    public event EventHandler<HotkeyTriggeredEventArgs>? HotkeyTriggered;

    public void Initialize()
    {
        lock (_lock)
        {
            if (_hwnd != IntPtr.Zero || _isDisposed)
            {
                return;
            }

            try
            {
                CreateMessageWindow();
                if (_isEnabled)
                {
                    RegisterAllHotkeysInternal();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Win32HotkeyService] Initialize failed: {ex.Message}");
            }
        }
    }

    public bool RegisterAllHotkeys()
    {
        lock (_lock)
        {
            return RegisterAllHotkeysInternal();
        }
    }

    private bool RegisterAllHotkeysInternal()
    {
        if (_hwnd == IntPtr.Zero || !_isEnabled)
        {
            return false;
        }

        UnregisterAllHotkeysInternal();

        bool allSucceeded = true;
        foreach (var binding in _settingsService.CurrentSettings.HotkeyBindings)
        {
            if (binding.IsEnabled)
            {
                bool success = RegisterHotkeyInternal(binding);
                if (!success)
                {
                    allSucceeded = false;
                }
            }
        }

        return allSucceeded;
    }

    public void UnregisterAllHotkeys()
    {
        lock (_lock)
        {
            UnregisterAllHotkeysInternal();
        }
    }

    private void UnregisterAllHotkeysInternal()
    {
        if (_hwnd == IntPtr.Zero)
        {
            _registeredBindings.Clear();
            return;
        }

        foreach (var kvp in _registeredBindings)
        {
            UnregisterHotKey(_hwnd, kvp.Key);
            kvp.Value.IsRegistered = false;
            kvp.Value.RegistrationError = null;
        }

        _registeredBindings.Clear();
    }

    public bool RegisterHotkey(HotkeyBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);

        lock (_lock)
        {
            return RegisterHotkeyInternal(binding);
        }
    }

    private bool RegisterHotkeyInternal(HotkeyBinding binding)
    {
        if (_hwnd == IntPtr.Zero || !_isEnabled || !binding.IsEnabled)
        {
            binding.IsRegistered = false;
            return false;
        }

        // Unregister existing registration for same action if present
        UnregisterHotkeyInternal(binding.Action);

        int id = _nextId++;
        uint modifiers = (uint)binding.Modifiers | 0x4000; // MOD_NOREPEAT (0x4000)

        bool success = RegisterHotKey(_hwnd, id, modifiers, binding.VirtualKey);
        if (success)
        {
            binding.IsRegistered = true;
            binding.RegistrationError = null;
            _registeredBindings[id] = binding;
            Debug.WriteLine($"[Win32HotkeyService] Registered hotkey '{binding.Name}' ({binding.DisplayString}) id={id}");
            return true;
        }
        else
        {
            int error = Marshal.GetLastWin32Error();
            binding.IsRegistered = false;
            binding.RegistrationError = $"Error 0x{error:X8} (Hotkey may already be registered by another application)";
            Debug.WriteLine($"[Win32HotkeyService] Failed to register hotkey '{binding.Name}': {binding.RegistrationError}");
            return false;
        }
    }

    public void UnregisterHotkey(HotkeyAction action)
    {
        lock (_lock)
        {
            UnregisterHotkeyInternal(action);
        }
    }

    private void UnregisterHotkeyInternal(HotkeyAction action)
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        var matching = _registeredBindings.FirstOrDefault(kvp => kvp.Value.Action == action);
        if (matching.Key != 0)
        {
            UnregisterHotKey(_hwnd, matching.Key);
            matching.Value.IsRegistered = false;
            matching.Value.RegistrationError = null;
            _registeredBindings.Remove(matching.Key);
            Debug.WriteLine($"[Win32HotkeyService] Unregistered hotkey '{matching.Value.Name}' id={matching.Key}");
        }
    }

    public void SetEnabled(bool isEnabled)
    {
        lock (_lock)
        {
            _isEnabled = isEnabled;
            if (_isEnabled)
            {
                RegisterAllHotkeysInternal();
            }
            else
            {
                UnregisterAllHotkeysInternal();
            }
        }
    }

    public void ReloadSettings()
    {
        lock (_lock)
        {
            _isEnabled = _settingsService.CurrentSettings.EnableGlobalHotkeys;
            if (_isEnabled)
            {
                RegisterAllHotkeysInternal();
            }
            else
            {
                UnregisterAllHotkeysInternal();
            }
        }
    }

    private void CreateMessageWindow()
    {
        _windowClassName = $"SwitchCast_HotkeyMsgWindow_{Guid.NewGuid():N}";
        _wndProcDelegate = CustomWndProc;

        var wndClass = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
            hInstance = GetModuleHandle(null),
            lpszClassName = _windowClassName
        };

        ushort atom = RegisterClassEx(ref wndClass);
        if (atom == 0)
        {
            int err = Marshal.GetLastWin32Error();
            Debug.WriteLine($"[Win32HotkeyService] RegisterClassEx failed error={err}");
            return;
        }

        _hwnd = CreateWindowEx(
            0,
            _windowClassName,
            "SwitchCastHotkeyListener",
            0,
            0, 0, 0, 0,
            HWND_MESSAGE,
            IntPtr.Zero,
            wndClass.hInstance,
            IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            Debug.WriteLine($"[Win32HotkeyService] CreateWindowEx failed error={err}");
        }
    }

    private IntPtr CustomWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            HotkeyBinding? binding;

            lock (_lock)
            {
                _registeredBindings.TryGetValue(id, out binding);
            }

            if (binding is not null)
            {
                Debug.WriteLine($"[Win32HotkeyService] Hotkey triggered: {binding.Name} ({binding.Action})");
                try
                {
                    HotkeyTriggered?.Invoke(this, new HotkeyTriggeredEventArgs(binding.Action, binding));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Win32HotkeyService] Exception handling hotkey: {ex.Message}");
                }
                return IntPtr.Zero;
            }
        }

        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            UnregisterAllHotkeysInternal();

            if (_hwnd != IntPtr.Zero)
            {
                DestroyWindow(_hwnd);
                _hwnd = IntPtr.Zero;
            }

            if (!string.IsNullOrEmpty(_windowClassName))
            {
                UnregisterClass(_windowClassName, GetModuleHandle(null));
                _windowClassName = string.Empty;
            }
        }
    }

    #region Win32 P/Invoke

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx([In] ref WNDCLASSEX lpwcx);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(
        uint dwExStyle,
        string lpClassName,
        string lpWindowName,
        uint dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    #endregion
}
