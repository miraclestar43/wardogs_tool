namespace WardogsTool.Core.Input;

/// <summary>
/// Left mouse button injection. Production: WindowsMouseInput (the same user32 mouse_event call
/// the Python tool makes). Tests: a recording fake.
/// </summary>
public interface IMouseInput
{
    void LeftDown();
    void LeftUp();
}

/// <summary>
/// Keyboard injection. Production: WindowsKeyboardInput (the same user32 keybd_event call the
/// Python tool makes). Tests: a recording fake.
/// </summary>
public interface IKeyboardInput
{
    /// <summary>
    /// Python's <c>char_to_vk</c>: VkKeyScanW, keeping only the low byte (the shift state is
    /// dropped, so "C" and "c" map to the same key). False when the character has no key.
    /// </summary>
    bool TryGetVirtualKey(char c, out byte virtualKey);

    void KeyDown(byte virtualKey);
    void KeyUp(byte virtualKey);
}
