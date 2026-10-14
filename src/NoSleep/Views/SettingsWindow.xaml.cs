using System.Windows;
using System.Windows.Controls;
using NoSleep.ViewModels;

namespace NoSleep.Views;

/// <summary>View only: binds <see cref="SettingsViewModel"/> and shows its validation messages.</summary>
internal sealed partial class SettingsWindow : Window
{
	private readonly SettingsViewModel _vm;

	public SettingsWindow(SettingsViewModel vm)
	{
		_vm = vm;
		DataContext = vm;
		InitializeComponent();
		vm.ValidationFailed += Warn;
		Closed += (_, _) => vm.ValidationFailed -= Warn;
	}

	private void OnSavePresets(object sender, RoutedEventArgs e)
	{
		// A row still being edited is not in the collection yet.
		PresetsGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);
		_vm.SavePresetsCommand.Execute(null);
	}

	private void Warn(string text) => MessageBox.Show(this, text, "NoSleep", MessageBoxButton.OK, MessageBoxImage.Warning);
}
