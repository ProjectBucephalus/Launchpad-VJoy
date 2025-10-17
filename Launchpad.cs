using Midi.Enums;
using Midi.Devices;
using vJoyInterfaceWrap;
using Windows.Media.Capture;
using Midi.Messages;

class Launchpad
{
    private const uint VJOY_ID = 1; // Valid range [1..16]
    private static readonly string[] LAUNCHPAD_NAMES = ["Launchpad", "LPMini", "LPX"];

    private readonly vJoy joystick = new();
    private InputDevice launchpadInput;
    private OutputDevice launchpadOutput;

    public Launchpad()
    {
        InitLaunchpad();
        InitJoystick();
    }

    private void SetPad(Pitch pitch, PadColor color) =>
        launchpadOutput.SendNoteOn(Channel.Channel1, pitch, (int)color);

    private void SetJoy(bool val, uint btnId) =>
        joystick.SetBtn(val, VJOY_ID, btnId);

    private static uint PitchToBtn(Pitch pitch) => ((uint)pitch) + 1;

    private void InitLaunchpad()
    {
        foreach (var device in DeviceManager.InputDevices)
        {
            if (Array.Exists(LAUNCHPAD_NAMES, device.Name.Contains))
            {
                Console.WriteLine("Found Input: " + device.Name);
                launchpadInput = (InputDevice)device;
                launchpadInput.Open();
            }
        }

        foreach (var device in DeviceManager.OutputDevices)
        {
            if (Array.Exists(LAUNCHPAD_NAMES, device.Name.Contains))
            {
                Console.WriteLine("Found Output: " + device.Name);
                launchpadOutput = (OutputDevice)device;
                launchpadOutput.Open();
            }
        }

        if (launchpadInput == null || launchpadOutput == null)
            ExitWith("Failed to find Launchpad.");
    }

    private void InitJoystick()
    {
        // Ensure vJoy enabled
        if (!joystick.vJoyEnabled()) ExitWith("vJoy driver not enabled: Failed Getting vJoy attributes.");

        // Check the state of the requested device
        switch (joystick.GetVJDStatus(VJOY_ID))
        {
            case VjdStat.VJD_STAT_OWN:
                Console.WriteLine("vJoy Device {0} is already owned by this feeder\n", VJOY_ID);
                break;
            case VjdStat.VJD_STAT_FREE:
                Console.WriteLine("vJoy Device {0} is free\n", VJOY_ID);
                if (joystick.AcquireVJD(VJOY_ID))
                {
                    Console.WriteLine("Acquired: vJoy device number {0}.\n", VJOY_ID);
                    joystick.ResetVJD(VJOY_ID);
                }
                break;
            case VjdStat.VJD_STAT_BUSY:
                ExitWith($"vJoy Device {VJOY_ID} is already owned by another feeder.");
                return;
            case VjdStat.VJD_STAT_MISS:
                ExitWith($"vJoy Device {VJOY_ID} is not installed or is disabled.");
                return;
            case VjdStat.VJD_STAT_UNKN:
                ExitWith($"vJoy Device {VJOY_ID} general error.");
                return;
        };
    }

    private void OnPress(NoteOnMessage msg)
    {
        var vel = msg.Velocity;
        var pitch = msg.Pitch;
        if (vel == 127)
        {
            SetPad(pitch, PadColor.FULL_GREEN);
            SetJoy(true, PitchToBtn(pitch));
        }
        else if (vel == 0)
        {
            SetPad(pitch, PadColor.FULL_ORANGE);
            SetJoy(false, PitchToBtn(pitch));
        }
    }

    public void BeginComms()
    {
        launchpadInput.NoteOn += OnPress;
        launchpadInput.StartReceiving(null);
    }

    private static void ExitWith(string msg)
    {
        Console.WriteLine(msg + " Press any key to exit");
        Console.ReadKey();
        Environment.Exit(1);
    }
}