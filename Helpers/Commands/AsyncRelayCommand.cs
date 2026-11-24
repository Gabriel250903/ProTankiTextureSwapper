using System.Windows.Input;
using Serilog;

namespace TextureSwapper.Helpers.Commands
{
    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<object?, Task> _execute;
        private readonly Predicate<object?>? _canExecute;
        private int _isExecuting;

        public AsyncRelayCommand(Func<object?, Task> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;

            CommandManager.RequerySuggested += (_, _) => RaiseCanExecuteChanged();
        }

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

            RaiseCanExecuteChanged();
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
                RaiseCanExecuteChanged();
            }
        }

        public event EventHandler? CanExecuteChanged;

        private void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
