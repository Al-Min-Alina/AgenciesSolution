using System.Windows;

namespace Agencies.Client.Dialogs
{
    public partial class ProgressDialog : Window
    {
        public string Message
        {
            get => tbMessage.Text;
            set => tbMessage.Text = value;
        }

        public string Details
        {
            get => tbDetails.Text;
            set => tbDetails.Text = value;
        }

        public bool IsIndeterminate
        {
            get => pbProgress.IsIndeterminate;
            set => pbProgress.IsIndeterminate = value;
        }

        public double ProgressValue
        {
            get => pbProgress.Value;
            set
            {
                pbProgress.IsIndeterminate = false;
                pbProgress.Value = value;
            }
        }

        public ProgressDialog()
        {
            InitializeComponent();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public void UpdateProgress(double value, string details = null)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressValue = value;
                if (!string.IsNullOrEmpty(details))
                {
                    Details = details;
                }
            });
        }

        public void UpdateMessage(string message)
        {
            Dispatcher.Invoke(() =>
            {
                Message = message;
            });
        }
    }
}