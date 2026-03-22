using System.Windows.Forms;

namespace LongYinUpdater;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        UpdateOptions options;
        try
        {
            options = UpdateOptions.Parse(args);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "龙胤立志传 Pro Max 更新器",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        Application.Run(new UpdaterForm(options));
    }
}
