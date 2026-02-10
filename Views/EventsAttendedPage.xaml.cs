using Library.Services;
using Library.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Library.Views
{
    public partial class EventsAttendedPage : ContentPage
    {
        public EventsAttendedPage()
        {
            InitializeComponent();

            // Resolve services from the MAUI DI container and set the view-model.
            // Cast Application.Current to your App class which exposes the Services property.
            var app = Application.Current as App;
            var services = app?.Services;

            var vm = new EventsAttendedViewModel(
                services?.GetService<IAuthService>(),
                services?.GetService<ILibraryService>(),
                services?.GetService<IDatabaseService>());

            BindingContext = vm;
        }
    }
}