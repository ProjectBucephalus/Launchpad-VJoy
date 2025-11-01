using Midi.Enums;
using Midi.Devices;
using vJoyInterfaceWrap;
using Midi.Messages;
using NetworkTables;
using NetworkTables.Tables;

class Launchpad
{
    private const string NT_TABLE_NAME = "LaunchPadColours";
    private const uint VJOY_ID = 2; // Valid range [1..16]
    private static readonly string[] LAUNCHPAD_NAMES = ["Launchpad", "LPMini", "LPX"];
    private readonly vJoy joystick = new();
    private InputDevice launchpadInput;
    private OutputDevice launchpadOutput;
    private readonly NetworkTable ntTable;

    //Pitches 8, 24, 40, 56, 72, 88, 104, 120 are DPad 0
    //Rows 0 and 1 are DPad 1
    //Rows 2-3 are DPads 2-3
    //Bottom 4 rows are all indiviudal buttons
    public Launchpad()
    {
        InitLaunchpad();
        InitJoystick();
        NetworkTable.SetClientMode();
        NetworkTable.SetPort(1735);
        NetworkTable.Initialize();

        ntTable = NetworkTable.GetTable(NT_TABLE_NAME); //new("Launchpad", "127.0.0.1");//, onNewTopicData: OnNTUpdate);
    }

    private void SetColour(Pitch pitch, PadColor color) =>
        launchpadOutput.SendNoteOn(Channel.Channel1, pitch, (int)color);

    private void SetBtn(uint btnId, bool val) =>
        joystick.SetBtn(val, VJOY_ID, btnId+1);

    private void SetPov(uint povId, int val) =>
        joystick.SetContPov(val*100, VJOY_ID, povId+1);

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

        Console.WriteLine(joystick.GetVJDButtonNumber(VJOY_ID));
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
        }
        ;
    }

    private void HandlePov(int vel, uint povId, int onVal)
    {
        if (vel == 127) 
            SetPov(povId, onVal);
        else if (vel == 0) 
            SetPov(povId, -1);
    }

    private void OnPress(NoteOnMessage msg)
    {
        int vel = msg.Velocity;
        var pitch = msg.Pitch;

        int nPitch = (int)pitch;
        int col = nPitch % 16;
        int row = nPitch / 16;

        if (col == 8)
            HandlePov(vel, 0, row);
        else if (row < 2)
            HandlePov(vel, 1, col + (row * 8));
        else if (row == 2)
            HandlePov(vel, 2, col);
        else if (row == 3)
            HandlePov(vel, 3, col);
        else
        {
            uint btn = (uint)(col + ((row - 4) * 8));
            if (vel == 127)
                SetBtn(btn, true);
            else if (vel == 0)
                SetBtn(btn, false);
        }

        if (vel == 127) 
            SetColour(pitch, PadColor.FULL_GREEN);
        else if (vel == 0) 
            SetColour(pitch, PadColor.DIM_AMBER);
    }

    private void ColourDemo()
    {
        SetSquare(0, PadColor.OFF);
        SetSquare(2, PadColor.DIM_GREEN);
        SetSquare(4, PadColor.MEDIUM_GREEN);
        SetSquare(6, PadColor.FULL_GREEN);

        SetSquare(32, PadColor.DIM_RED);
        SetSquare(34, PadColor.DIM_AMBER);
        SetSquare(36, PadColor.MEDIUM_YELLOW_GREEN);
        SetSquare(38, PadColor.FULL_YELLOW_GREEN);

        SetSquare(64, PadColor.MEDIUM_RED);
        SetSquare(66, PadColor.MEDIUM_ORANGE);
        SetSquare(68, PadColor.MEDIUM_AMBER);
        SetSquare(70, PadColor.FULL_YELLOW);

        SetSquare(96, PadColor.FULL_RED);
        SetSquare(98, PadColor.FULL_ORANGE_RED);
        SetSquare(100, PadColor.FULL_ORANGE);
        SetSquare(102, PadColor.FULL_AMBER);

        void SetSquare(uint topLeft, PadColor color)
        {
            SetColour((Pitch)topLeft, color);
            SetColour((Pitch)topLeft + 1, color);
            SetColour((Pitch)topLeft + 16, color);
            SetColour((Pitch)topLeft + 17, color);
        }
    }

    private void Prt5985(byte speed) =>
        launchpadOutput.SendSysEx([0xF0, 0x00, 0x20, 0x29, 0x09, (byte)PadColor.FULL_RED + 64, speed, 0x35, 0x39, 0x38, 0x35, 0xF7]);

    private void OnNTUpdate(ITable source, string key, Value value, NotifyFlags flags)
    {
        int nKey = int.Parse(key);
        int btnId = (nKey % 9) + ((nKey / 9) * 16);
        Console.WriteLine(btnId);
        SetColour((Pitch)btnId, (PadColor)value.GetDouble());
    }    

    public void BeginComms()
    {
        launchpadInput.NoteOn += OnPress;
        launchpadInput.StartReceiving(null);
        ntTable.AddTableListener(OnNTUpdate);        

        foreach (Pitch p in Enum.GetValues<Pitch>()) SetColour(p, PadColor.DIM_AMBER);
    }

    private static void ExitWith(string msg)
    {
        Console.WriteLine(msg + " Press any key to exit");
        Console.ReadKey();
        Environment.Exit(1);
    }
}