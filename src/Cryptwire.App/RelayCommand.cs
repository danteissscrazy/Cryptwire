using System.Windows.Input;

namespace Cryptwire.App;

/// <summary>Un comando de MVVM que solo llama a una acción: lo justo para los botones de los ajustes.</summary>
internal sealed class RelayCommand(Action<object?> execute) : ICommand
{
    public RelayCommand(Action execute)
        : this(_ => execute())
    {
    }

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => execute(parameter);
}
