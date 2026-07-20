using Serilog;
using System.Windows.Input;

namespace TextureSwapper.Helpers
{
    public class AsyncRelayCommand(Func<object?, Task> execute, Predicate<object?>? canExecute = null) : ICommand
    {
        private readonly Func<object?, Task> _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        private readonly Predicate<object?>? _canExecute = canExecute;
        private int _isExecuting;

        public bool CanExecute(object? parameter)
        {
            return _isExecuting == 0 && (_canExecute == null || _canExecute(parameter));
        }

        public async void Execute(object? parameter)
        {
            if (Interlocked.CompareExchange(ref _isExecuting, 1, 0) != 0)
            {
                return;
            }

            CommandManager.InvalidateRequerySuggested();
            try
            {
                await _execute(parameter);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unhandled exception in async command execution.");
            }
            finally
            {
                _ = Interlocked.Exchange(ref _isExecuting, 0);
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
