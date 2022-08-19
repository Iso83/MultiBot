namespace MultiBot
{
    /// <summary>
    /// Interakční logika pro MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            var d = new FoE.Farmer.Library.Windows.MainPage(this);
            MainFrame.Navigate(d);
        }
    }
}
