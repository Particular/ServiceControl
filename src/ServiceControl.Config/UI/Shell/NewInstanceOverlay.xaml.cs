using System.Windows.Controls;
using System.Windows.Input;

namespace ServiceControl.Config.UI.Shell
{
	using System;

	/// <summary>
	/// Interaction logic for NewInstanceOverlay.xaml
	/// </summary>
	public partial class NewInstanceOverlay
	{
		public NewInstanceOverlay()
		{
			InitializeComponent();
		}

		private void ViewPreviewMouseDown(object sender, MouseButtonEventArgs e)
		{
			Console.WriteLine(sender.GetType().Name);
		}
	}
}
