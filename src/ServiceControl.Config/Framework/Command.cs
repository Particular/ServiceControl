using System;
using System.Threading.Tasks;
using ServiceControl.Config.Framework.Commands;

namespace ServiceControl.Config.Framework
{
    static class Command
    {
        public static ICommand Create(Action executeMethod)
        {
            return new DelegateCommand(_ => executeMethod());
        }

        public static ICommand Create(Action executeMethod, Func<bool> canExecuteMethod)
        {
            return new DelegateCommand(_ => executeMethod(), _ => canExecuteMethod());
        }

        public static ICommand<T> Create<T>(Action<T> executeMethod)
        {
            return new DelegateCommand<T>(executeMethod);
        }

        public static ICommand<T> Create<T>(Action<T> executeMethod, Func<T, bool> canExecuteMethod)
        {
            return new DelegateCommand<T>(executeMethod, canExecuteMethod);
        }

        public static IAsyncCommand Create(Func<Task> executeMethod)
        {
            return new AwaitableDelegateCommand(_ => executeMethod());
        }

        public static IAsyncCommand Create(Func<Task> executeMethod, Func<bool> canExecuteMethod)
        {
            return new AwaitableDelegateCommand(_ => executeMethod(), _ => canExecuteMethod());
        }

        public static IAsyncCommand<T> Create<T>(Func<T, Task> executeMethod)
        {
            return new AwaitableDelegateCommand<T>(executeMethod);
        }

        public static IAsyncCommand<T> Create<T>(Func<T, Task> executeMethod, Func<T, bool> canExecuteMethod)
        {
            return new AwaitableDelegateCommand<T>(executeMethod, canExecuteMethod);
        }
    }
}