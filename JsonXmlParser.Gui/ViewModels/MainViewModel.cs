using System.IO;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace JsonXmlParser.Gui.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _textContent = string.Empty;

        [ObservableProperty]
        private object _treeContent;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [RelayCommand]
        private void OpenFile()
        {
            // Implement file opening logic
        }

        [RelayCommand]
        private void SaveFile()
        {
            // Implement file saving logic
        }

        [RelayCommand]
        private void GenerateRandomData()
        {
            // Implement random data generation logic
        }

        partial void OnSearchQueryChanged(string value)
        {
            // Implement search functionality
        }
    }
}