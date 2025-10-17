class Program
{
    static void Main()
    {
        Launchpad launchpad = new();
        launchpad.BeginComms();
        while (true) ;
    }
}
