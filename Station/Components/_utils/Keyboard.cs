using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows.Forms;
using LeadMeLabsLibrary;
using Newtonsoft.Json.Linq;
using Station.Components._notification;
using Station.Core;
using Application = System.Windows.Application;

namespace Station.Components._utils;


public static class Keyboard
{
    /// <summary>
    /// Determine what component is being requested (Mouse or Keyboard) then pass the action to the appropriate handler
    /// function.
    /// </summary>
    /// <param name="jObjectData">A string of an object containing the information.</param>
    public static void DetermineAction(string jObjectData)
    {
        JObject requestData = JObject.Parse(jObjectData);
        var component = requestData.GetValue("Component")?.ToString();
        
        switch (component)
        {
            case "Keyboard":
                KeyboardAction(requestData);
                break;
            
            case "Mouse":
                MouseAction(requestData);
                break;
            
            default:
                MockConsole.WriteLine($"Unknown keyboard action requested: {jObjectData}", Enums.LogLevel.Error);
                break;
        }
    }

    #region Keyboard Actions
    /// <summary>
    /// Handles keyboard actions based on the provided request data. 
    /// The method interprets the action type and delegates the handling to the appropriate method.
    /// </summary>
    /// <param name="requestData">
    /// A JSON object containing the action details. 
    /// The object must have an "Action" field specifying the type of action and a "Details" field with additional information.
    /// </param>
    /// <remarks>
    /// The method currently supports two types of actions: "Control" and "Character".
    /// If the action type is "Control", it calls <see cref="HandleKeyboardControl"/> with the details.
    /// If the action type is "Character", it calls <see cref="HandleKeyboardCharacter"/> with the details.
    /// If the "Details" field is null, the method returns immediately without performing any action.
    /// </remarks>
    private static void KeyboardAction(JObject requestData)
    {
        var action = requestData.GetValue("Action")?.ToString();
        var details = requestData.GetValue("Details");
        if (details == null) return;
        
        switch (action)
        {
            case null:
                break;
            
            case "Control":
                HandleKeyboardControl((JObject)details);
                break;
            
            case "Character":
                HandleKeyboardCharacter((JObject)details);
                break;
        }
    }

    /// <summary>
    /// Handles specific keyboard control actions based on the provided action details.
    /// The method interprets the key and performs the corresponding keyboard action using SendKeys.SendWait or Process.Start.
    /// </summary>
    /// <param name="action">
    /// A JSON object containing the key details. 
    /// The object must have a "Key" field specifying the control key to be simulated.
    /// </param>
    /// <remarks>
    /// The method supports the following control keys:
    /// - "Tab": Simulates the Tab key.
    /// - "Windows": Simulates the Windows key by sending Ctrl+Esc.
    /// - "Space": Simulates the Space bar.
    /// - "Backspace": Simulates the Backspace key.
    /// - "Enter": Simulates the Enter key.
    /// - "TaskManager": Opens the Task Manager.
    /// - "ArrowLeft": Simulates the Left arrow key.
    /// - "ArrowUp": Simulates the Up arrow key.
    /// - "ArrowDown": Simulates the Down arrow key.
    /// - "ArrowRight": Simulates the Right arrow key.
    /// If the "Key" field is null or unrecognized, the method returns without performing any action.
    /// </remarks>
    private static void HandleKeyboardControl(JObject action)
    {
        string? key = action.GetValue("Key")?.ToString();
        switch (key)
        {
            case null:
                break;
            
            case "Tab":
                SendKeys.SendWait("{TAB}");
                break;
            
            case "Windows":
                SendKeys.SendWait("^{ESC}");
                break;
            
            case "Space":
                SendKeys.SendWait(" ");
                break;
            
            case "Backspace":
                SendKeys.SendWait("{BACKSPACE}");
                break;
            
            case "Enter":
                SendKeys.SendWait("{ENTER}");
                break;
            
            case "TaskManager":
                //Can only perform this with elevated privileges
                if (IsRunningAsAdministrator())
                {
                    Process.Start("taskmgr.exe");
                }
                else
                {
                    if (NotifyIconWrapper.Instance == null) break;
                    Application.Current.Dispatcher.Invoke(delegate
                    {
                        
                        NotifyIconWrapper.Instance.NotifyRequest = new NotifyIconWrapper.NotifyRequestRecord
                        {
                            Title = "Virtual Keyboard",
                            Text = "This action requires the Station to have Admin privileges.",
                            Duration = 5000
                        };
                    });
                }
                break;
            
            //Arrow keys
            case "ArrowLeft":
                SendKeys.SendWait("{LEFT}");
                break;
            
            case "ArrowUp":
                SendKeys.SendWait("{UP}");
                break;
            
            case "ArrowDown":
                SendKeys.SendWait("{DOWN}");
                break;
            
            case "ArrowRight":
                SendKeys.SendWait("{RIGHT}");
                break;
        }
    }

    /// <summary>
    /// Handles keyboard character actions by simulating the pressing of the specified key.
    /// The method sends the key to the active application using SendKeys.SendWait.
    /// </summary>
    /// <param name="action">
    /// A JSON object containing the key details. 
    /// The object must have a "Key" field specifying the character or key to be sent.
    /// </param>
    /// <remarks>
    /// If the "Key" field is null, the method will send an empty string, resulting in no action.
    /// </remarks>
    private static void HandleKeyboardCharacter(JObject action)
    {
        string? key = action.GetValue("Key")?.ToString();
        switch (key)
        {
            case "Percent": //%
                SendKeys.SendWait("{%}");
                break;
            
            case "Caret": //^
                SendKeys.SendWait("{^}");
                break;
            
            case "BracketLeft": //(
                SendKeys.SendWait("{(}");
                break;
            
            case "BracketRight": //)
                SendKeys.SendWait("{)}");
                break;
            
            case "CurlyLeft": //{
                SendKeys.SendWait("{{}");
                break;
            
            case "CurlyRight": //}
                SendKeys.SendWait("{}}");
                break;
            
            case "Plus": //+
                SendKeys.SendWait("{+}");
                break;
            
            default:
                SendKeys.SendWait(key);
                break;
        }
        
    }
    
    private static bool IsRunningAsAdministrator()
    {
        WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
    #endregion
    
    #region Mouse Actions
    /// <summary>
    /// Handles mouse actions based on the provided request data.
    /// The method interprets the action type and delegates the handling to the appropriate method for moving or clicking the cursor.
    /// </summary>
    /// <param name="requestData">
    /// A JSON object containing the action details.
    /// The object must have an "Action" field specifying the type of mouse action and a "Details" field with additional information.
    /// </param>
    /// <remarks>
    /// The method currently supports two types of actions: "Move" and "Click".
    /// If the action type is "Move", it calls <see cref="MoveCursor"/> with the details.
    /// If the action type is "Click", it calls <see cref="ClickCursor"/> with the details.
    /// If the "Details" field is null, the method returns immediately without performing any action.
    /// </remarks>
    private static void MouseAction(JObject requestData)
    {
        var action = requestData.GetValue("Action")?.ToString();
        var details = requestData.GetValue("Details");
        if (details == null) return;
        
        switch (action)
        {
            case null:
                break;
            
            case "Move":
                MoveCursor((JObject)details);
                break;
            
            case "Click":
                ClickCursor((JObject)details);
                break;
        }
    }
    
    /// <summary>
    /// Moves the cursor based on the specified delta values for X and Y.
    /// The method adjusts the current cursor position by the provided delta values.
    /// </summary>
    /// <param name="action">
    /// A JSON object containing the movement details. 
    /// The object must have "MoveX" and "MoveY" fields specifying the delta values for moving the cursor.
    /// </param>
    /// <remarks>
    /// If either "MoveX" or "MoveY" is null, the method returns immediately without moving the cursor.
    /// </remarks>
    private static void MoveCursor(JObject action)
    {
        var deltaX = action.GetValue("MoveX");
        var deltaY = action.GetValue("MoveY");
        if (deltaX == null || deltaY == null) return;
        
        Cursor.Position = new System.Drawing.Point(
            Cursor.Position.X + (int)deltaX,
            Cursor.Position.Y + (int)deltaY);
    }

    /// <summary>
    /// Handles mouse click actions based on the specified button.
    /// The method interprets the button type and delegates the click action to the <see cref="MouseClick"/> method.
    /// </summary>
    /// <param name="action">
    /// A JSON object containing the click details. 
    /// The object must have a "Button" field specifying which mouse button to click ("Left" or "Right").
    /// </param>
    /// <remarks>
    /// If the "Button" field is null or not one of the supported values ("Left" or "Right"), the method returns immediately without performing any action.
    /// </remarks>
    private static void ClickCursor(JObject action)
    {
        string? button = action.GetValue("Button")?.ToString();
        switch (button)
        {
            case null:
                return;
            
            case "Left":
            case "Right":
                MouseClick(button);
                break;
        }
    }
    #endregion

    #region Mouse Input
    [StructLayout(LayoutKind.Sequential)]
    struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    struct InputUnion
    {
        [FieldOffset(0)]
        public MOUSEINPUT mi;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    const uint INPUT_MOUSE = 0;
    const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    const uint MOUSEEVENTF_LEFTUP = 0x0004;
    const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    const uint MOUSEEVENTF_RIGHTUP = 0x0010;

    [DllImport("user32.dll", SetLastError = true)]
    static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    static extern IntPtr GetMessageExtraInfo();
    
    private static void MouseClick(string button)
    {
        INPUT[] inputs = new INPUT[2];

        uint down, up;
        if (button.ToLower() == "left")
        {
            down = MOUSEEVENTF_LEFTDOWN;
            up = MOUSEEVENTF_LEFTUP;
        }
        else if (button.ToLower() == "right")
        {
            down = MOUSEEVENTF_RIGHTDOWN;
            up = MOUSEEVENTF_RIGHTUP;
        }
        else
        {
            throw new ArgumentException("Invalid button type.");
        }

        // Mouse down input
        inputs[0] = new INPUT
        {
            type = INPUT_MOUSE,
            u = new InputUnion
            {
                mi = new MOUSEINPUT
                {
                    dwFlags = down,
                    dwExtraInfo = GetMessageExtraInfo()
                }
            }
        };

        // Mouse up input
        inputs[1] = new INPUT
        {
            type = INPUT_MOUSE,
            u = new InputUnion
            {
                mi = new MOUSEINPUT
                {
                    dwFlags = up,
                    dwExtraInfo = GetMessageExtraInfo()
                }
            }
        };

        // Send the inputs
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
    }
    #endregion
}