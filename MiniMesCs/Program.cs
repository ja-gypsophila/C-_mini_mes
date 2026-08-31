namespace MiniMesCs;

// 프로그램이 시작되는 지점.
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Form1 창을 띄우고, 창이 닫힐 때까지 프로그램을 유지한다.
        Application.Run(new Form1());
    }
}
