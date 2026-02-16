using Microsoft.Maui.Controls;
using System.Reflection;
using System.Windows.Input;

namespace Library.Helpers
{
    public class EventToCommandBehavior : Behavior<VisualElement>
    {
        public static readonly BindableProperty EventNameProperty =
            BindableProperty.Create(nameof(EventName), typeof(string), typeof(EventToCommandBehavior), null);

        public static readonly BindableProperty CommandProperty =
            BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(EventToCommandBehavior), null);

        public static readonly BindableProperty CommandParameterProperty =
            BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(EventToCommandBehavior), null);

        public string EventName
        {
            get => (string)GetValue(EventNameProperty);
            set => SetValue(EventNameProperty, value);
        }

        public ICommand Command
        {
            get => (ICommand)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        public object CommandParameter
        {
            get => GetValue(CommandParameterProperty);
            set => SetValue(CommandParameterProperty, value);
        }

        protected override void OnAttachedTo(VisualElement bindable)
        {
            base.OnAttachedTo(bindable);
            var eventInfo = bindable.GetType().GetRuntimeEvent(EventName);
            if (eventInfo == null) return;

            var methodInfo = typeof(EventToCommandBehavior).GetMethod(nameof(OnEvent),
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var handler = Delegate.CreateDelegate(eventInfo.EventHandlerType, this, methodInfo);
            eventInfo.AddEventHandler(bindable, handler);
        }

        private void OnEvent(object sender, object e)
        {
            if (Command?.CanExecute(CommandParameter) == true)
            {
                Command.Execute(CommandParameter);
            }
        }
    }
}