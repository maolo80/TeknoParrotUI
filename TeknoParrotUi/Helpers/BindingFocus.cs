using System.Linq;
using System.Windows;

namespace TeknoParrotUi.Helpers
{
    public static class BindingFocus
    {
        /// <summary>
        /// Window whose focused text box receives a pressed binding. Windows[0] is
        /// not always the main window: debugger tooling and first-run dialogs can
        /// come first, and then every press was dropped.
        /// </summary>
        public static Window Window =>
            Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ??
            Application.Current.MainWindow;
    }
}
