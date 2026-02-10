using Library.Models;
using System.ComponentModel;
using System.Windows.Input;
using System;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace Library.ViewModels
{
    public class BranchDetailsViewModel : INotifyPropertyChanged
    {
        public Branch Branch { get; }

        public ICommand OpenMapCommand { get; }
        public ICommand CallCommand { get; }
        public ICommand EmailCommand { get; }

        public BranchDetailsViewModel(Branch branch)
        {
            Branch = branch ?? throw new ArgumentNullException(nameof(branch));

            OpenMapCommand = new Command(async () => await OpenMapAsync());
            CallCommand = new Command(async () => await CallAsync());
            EmailCommand = new Command(async () => await EmailAsync());
        }

        private async Task OpenMapAsync()
        {
            var url = Branch.GetMapsUrl();
            if (string.IsNullOrWhiteSpace(url)) return;
            try
            {
                await Launcher.OpenAsync(new Uri(url));
            }
            catch { /* handle / log as needed */ }
        }

        private async Task CallAsync()
        {
            if (string.IsNullOrWhiteSpace(Branch.Phone)) return;
            try
            {
                // Use tel: scheme - platform will choose dialer
                await Launcher.OpenAsync(new Uri($"tel:{Branch.Phone}"));
            }
            catch { /* handle / log as needed */ }
        }

        private async Task EmailAsync()
        {
            if (string.IsNullOrWhiteSpace(Branch.Email)) return;
            try
            {
                await Launcher.OpenAsync(new Uri($"mailto:{Branch.Email}"));
            }
            catch { /* handle / log as needed */ }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void RaisePropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}