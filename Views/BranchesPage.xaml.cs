
using Library.Models;
using Library.Services;
using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;

namespace Library.Views;

public partial class BranchesPage : ContentPage
{
    private readonly ILibraryService _libraryService;
    public ObservableCollection<Branch> Branches { get; } = new();

    public BranchesPage(ILibraryService libraryService)
    {
        InitializeComponent();
        _libraryService = libraryService;
        BranchesCollection.ItemsSource = Branches;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadBranchesAsync();
    }

    private async Task LoadBranchesAsync()
    {
        try
        {
            LoadingIndicator.IsVisible = true;
            LoadingIndicator.IsRunning = true;

            var list = await _libraryService.GetBranchesAsync();
            Branches.Clear();
            if (list != null)
            {
                foreach (var b in list)
                    Branches.Add(b);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load branches: {ex.Message}");
            await DisplayAlert("Error", "Unable to load branches", "OK");
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
        }
    }

    private async void BranchesCollection_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = e.CurrentSelection.FirstOrDefault() as Branch;
        if (selected == null) return;

        // Clear selection
        ((CollectionView)sender).SelectedItem = null;

        // Navigate to details by constructing details page with the selected branch
        // BranchDetailsPage has a constructor that accepts Branch
        await Shell.Current.Navigation.PushAsync(new BranchDetailsPage(selected));
    }
}