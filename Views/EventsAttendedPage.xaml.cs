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
            BindingContext = App.Current.Handler.MauiContext.Services
                   .GetRequiredService<EventsAttendedViewModel>();
        }
    }
}