using System;
using System.Windows.Input;

namespace vrcosc_magicchatbox.Classes
{
    public class CopyToClipboardCommand : ICommand
    {
        public event EventHandler CanExecuteChanged;
        private Action _execute;

        public CopyToClipboardCommand(Action execute)
        {
            _execute = execute;
        }

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            _execute.Invoke();
        }
    }
}
