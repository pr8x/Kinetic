using System;
using System.Windows.Input;

namespace Kinetic
{
    public abstract class KineticCommand<TParameter, TResult> : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public abstract bool CanExecute(TParameter parameter);

        public abstract TResult Execute(TParameter parameter);

        bool ICommand.CanExecute(object? parameter)
        {
            throw new NotImplementedException();
        }

        void ICommand.Execute(object? parameter)
        {
            throw new NotImplementedException();
        }

        private protected void OnCanExecuteChanged() =>
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    internal interface IKineticCommandHandler<TState, TParameter, TResult>
    {
        TState State { get; set; }

        bool CanExecute(TParameter parameter);
        TResult Execute(TParameter parameter);
    }

    internal struct KineticCommandHandler<TExecute, TEnabled, TParameter, TResult> : IKineticCommandHandler<bool, TParameter, TResult>
        where TExecute : struct, IKineticFunction<TParameter, TResult>
        where TEnabled : struct, IKineticFunction<TParameter, bool>
    {
        private readonly TExecute _execute;
        private readonly TEnabled _enabled;

        public KineticCommandHandler(TExecute execute, TEnabled enabled)
        {
            _execute = execute;
            _enabled = enabled;

            State = false;
        }

        public bool State { get; set; }

        public bool CanExecute(TParameter parameter) =>
            State && _enabled.Invoke(parameter);

        public TResult Execute(TParameter parameter) =>
            State && _enabled.Invoke(parameter)
            ? _execute.Invoke(parameter)
            : throw new InvalidOperationException();
    }

    internal struct KineticCommandHandler<TExecute, TEnabled, TState, TParameter, TResult> : IKineticCommandHandler<TState, TParameter, TResult>
        where TExecute : struct, IKineticFunction<TState, TParameter, TResult>
        where TEnabled : struct, IKineticFunction<TState, TParameter, bool>
    {
        private readonly TExecute _execute;
        private readonly TEnabled _enabled;

        public KineticCommandHandler(TExecute execute, TEnabled enabled)
        {
            _execute = execute;
            _enabled = enabled;

            State = default!;
        }

        public TState State { get; set; }

        public bool CanExecute(TParameter parameter) =>
            _enabled.Invoke(State, parameter);

        public TResult Execute(TParameter parameter) =>
            _enabled.Invoke(State, parameter)
            ? _execute.Invoke(State, parameter)
            : throw new InvalidOperationException();
    }

    internal sealed class KineticCommand<THandler, TState, TParameter, TResult> : KineticCommand<TParameter, TResult>, IObserver<TState>
        where THandler : struct, IKineticCommandHandler<TState, TParameter, TResult>
    {
        private THandler _handler;
        private IDisposable? _state;

        public KineticCommand(THandler handler, TState state)
        {
            _handler = handler;
            _handler.State = state;
        }

        public KineticCommand(THandler handler, IObservable<TState>? state)
        {
            _handler = handler;
            _state = state?.Subscribe(this);
        }

        public override bool CanExecute(TParameter parameter) =>
            _handler.CanExecute(parameter);

        public override TResult Execute(TParameter parameter) =>
            _handler.Execute(parameter);

        public void OnNext(TState value)
        {
            _handler.State = value;
            OnCanExecuteChanged();
        }

        public void OnError(Exception exception) { }
        public void OnCompleted() { }
    }
    
    public static class KineticCommand
    {
        public static KineticCommand<Unit, Unit> Create(
            Action execute) =>
            Create(Execute(execute), EnabledAlways(), NoState);

        public static KineticCommand<Unit, Unit> Create(
            Action execute, Func<bool> canExecute) =>
            Create(Execute(execute), Enabled(canExecute), NoState);

        public static KineticCommand<Unit, Unit> Create<TState>(
            TState state, Action<TState> execute) =>
            Create(Execute(execute), EnabledAlways<TState>(), state);

        public static KineticCommand<Unit, Unit> Create<TState>(
            TState state, Action<TState> execute, Func<TState, bool> canExecute) =>
            Create(Execute(execute), Enabled(canExecute), state);

        public static KineticCommand<Unit, Unit> Create<TState>(
            IObservable<TState> state, Action<TState> execute) =>
            Create(Execute(execute), EnabledAlways<TState>(), state);

        public static KineticCommand<Unit, Unit> Create<TState>(
            IObservable<TState> state, Action<TState> execute, Func<TState, bool> canExecute) =>
            Create(Execute(execute), Enabled(canExecute), state);

        public static KineticCommand<Unit, TResult> Create<TResult>(
            Func<TResult> execute) =>
            WithResult<TResult>.Create(Execute(execute), EnabledAlways<Unit>(), NoState);

        public static KineticCommand<Unit, TResult> Create<TResult>(
            Func<TResult> execute, Func<bool> canExecute) =>
            WithResult<TResult>.Create(Execute(execute), Enabled(canExecute), NoState);

        public static KineticCommand<Unit, TResult> Create<TState, TResult>(
            TState state, Func<TState, TResult> execute) =>
            WithResult<TResult>.Create(Execute(execute), EnabledAlways<TState>(), state);

        public static KineticCommand<Unit, TResult> Create<TState, TResult>(
            TState state, Func<TState, TResult> execute, Func<TState, bool> canExecute) =>
            WithResult<TResult>.Create(Execute(execute), Enabled(canExecute), state);

        public static KineticCommand<Unit, TResult> Create<TState, TResult>(
            IObservable<TState> state, Func<TState, TResult> execute) =>
            WithResult<TResult>.Create(Execute(execute), EnabledAlways<TState>(), state);

        public static KineticCommand<Unit, TResult> Create<TState, TResult>(
            IObservable<TState> state, Func<TState, TResult> execute, Func<TState, bool> canExecute) =>
            WithResult<TResult>.Create(Execute(execute), Enabled(canExecute), state);

        private static Unit NoState => default;

        private static ExecuteAction Execute(Action execute) => new(execute);
        private static ExecuteAction<TState> Execute<TState>(Action<TState> execute) => new(execute);

        private static Function<TResult> Execute<TResult>(Func<TResult> execute) => new(execute);
        private static Function<TState, TResult> Execute<TState, TResult>(Func<TState, TResult> execute) => new(execute);

        private static Function<bool> Enabled(Func<bool> enabled) => new(enabled);
        private static Function<TState, bool> Enabled<TState>(Func<TState, bool> enabled) => new(enabled);

        private static EnabledAlwaysFunction<Unit> EnabledAlways() => default;
        private static EnabledAlwaysFunction<TState> EnabledAlways<TState>() => default;

        private readonly struct ExecuteAction : IKineticFunction<Unit, Unit, Unit>
        {
            public readonly Action Execute;
            public ExecuteAction(Action execute) => Execute = execute;

            public Unit Invoke(Unit state, Unit parameter)
            {
                Execute();
                return default;
            }
        }

        private readonly struct ExecuteAction<TState> : IKineticFunction<TState, Unit, Unit>
        {
            public readonly Action<TState> Execute;
            public ExecuteAction(Action<TState> execute) => Execute = execute;

            public Unit Invoke(TState state, Unit parameter)
            {
                Execute(state);
                return default;
            }
        }

        private readonly struct Function<TResult> : IKineticFunction<Unit, Unit, TResult>
        {
            public readonly Func<TResult> Method;
            public Function(Func<TResult> method) => Method = method;
            public TResult Invoke(Unit state, Unit parameter) => Method();
        }

        private readonly struct Function<TState, TResult> : IKineticFunction<TState, Unit, TResult>
        {
            public readonly Func<TState, TResult> Method;
            public Function(Func<TState, TResult> method) => Method = method;
            public TResult Invoke(TState state, Unit parameter) => Method(state);
        }

        private readonly struct EnabledAlwaysFunction<TState> : IKineticFunction<TState, Unit, bool>
        {
            public bool Invoke(TState state, Unit parameter) => true;
        }

        private static KineticCommand<Unit, Unit> Create<TExecute, TEnabled, TState>(TExecute execute, TEnabled enabled, TState state)
            where TExecute : struct, IKineticFunction<TState, Unit, Unit>
            where TEnabled : struct, IKineticFunction<TState, Unit, bool>
        {
            return new KineticCommand<KineticCommandHandler<TExecute, TEnabled, TState, Unit, Unit>, TState, Unit, Unit>(
                handler: new(execute, enabled), state);
        }

        private static KineticCommand<Unit, Unit> Create<TExecute, TEnabled, TState>(TExecute execute, TEnabled enabled, IObservable<TState> state)
            where TExecute : struct, IKineticFunction<TState, Unit, Unit>
            where TEnabled : struct, IKineticFunction<TState, Unit, bool>
        {
            return new KineticCommand<KineticCommandHandler<TExecute, TEnabled, TState, Unit, Unit>, TState, Unit, Unit>(
                handler: new(execute, enabled), state);
        }

        private static class WithResult<TResult>
        {
            public static KineticCommand<Unit, TResult> Create<TExecute, TEnabled, TState>(TExecute execute, TEnabled enabled, TState state)
                where TExecute : struct, IKineticFunction<TState, Unit, TResult>
                where TEnabled : struct, IKineticFunction<TState, Unit, bool>
            {
                return new KineticCommand<KineticCommandHandler<TExecute, TEnabled, TState, Unit, TResult>, TState, Unit, TResult>(
                    handler: new(execute, enabled), state);
            }
            
            public static KineticCommand<Unit, TResult> Create<TExecute, TEnabled, TState>(TExecute execute, TEnabled enabled, IObservable<TState> state)
                where TExecute : struct, IKineticFunction<TState, Unit, TResult>
                where TEnabled : struct, IKineticFunction<TState, Unit, bool>
            {
                return new KineticCommand<KineticCommandHandler<TExecute, TEnabled, TState, Unit, TResult>, TState, Unit, TResult>(
                    handler: new(execute, enabled), state);
            }
        }

        public static bool CanExecute<TResult>(this KineticCommand<Unit, TResult> command) =>
            command.CanExecute(default);

        public static TResult Execute<TResult>(this KineticCommand<Unit, TResult> command) =>
            command.Execute(default);
    }

    public static class KineticCommand<TParameter>
    {
        public static KineticCommand<TParameter, Unit> Create(
            Action<TParameter> execute) =>
            Create(Execute(execute), EnabledAlways(), NoState);

        public static KineticCommand<TParameter, Unit> Create(
            Action<TParameter> execute, Func<TParameter, bool> canExecute) =>
            Create(Execute(execute), Enabled(canExecute), NoState);

        public static KineticCommand<TParameter, Unit> Create<TState>(
            TState state, Action<TState, TParameter> execute) =>
            Create(Execute(execute), EnabledAlways<TState>(), state);

        public static KineticCommand<TParameter, Unit> Create<TState>(
            TState state, Action<TState, TParameter> execute, Func<TState, TParameter, bool> canExecute) =>
            Create(Execute(execute), Enabled(canExecute), state);

        public static KineticCommand<TParameter, Unit> Create<TState>(
            IObservable<TState> state, Action<TState, TParameter> execute) =>
            Create(Execute(execute), EnabledAlways<TState>(), state);

        public static KineticCommand<TParameter, Unit> Create<TState>(
            IObservable<TState> state, Action<TState, TParameter> execute, Func<TState, TParameter, bool> canExecute) =>
            Create(Execute(execute), Enabled(canExecute), state);
            
        public static KineticCommand<TParameter, TResult> Create<TResult>(
            Func<TParameter, TResult> execute) =>
            WithResult<TResult>.Create(Execute(execute), EnabledAlways(), NoState);

        public static KineticCommand<TParameter, TResult> Create<TResult>(
            Func<TParameter, TResult> execute, Func<TParameter, bool> canExecute) =>
            WithResult<TResult>.Create(Execute(execute), Enabled(canExecute), NoState);

        public static KineticCommand<TParameter, TResult> Create<TState, TResult>(
            TState state, Func<TState, TParameter, TResult> execute) =>
            WithResult<TResult>.Create(Execute(execute), EnabledAlways<TState>(), state);

        public static KineticCommand<TParameter, TResult> Create<TState, TResult>(
            TState state, Func<TState, TParameter, TResult> execute, Func<TState, TParameter, bool> canExecute) =>
            WithResult<TResult>.Create(Execute(execute), Enabled(canExecute), state);

        public static KineticCommand<TParameter, TResult> Create<TState, TResult>(
            IObservable<TState> state, Func<TState, TParameter, TResult> execute) =>
            WithResult<TResult>.Create(Execute(execute), EnabledAlways<TState>(), state);

        public static KineticCommand<TParameter, TResult> Create<TState, TResult>(
            IObservable<TState> state, Func<TState, TParameter, TResult> execute, Func<TState, TParameter, bool> canExecute) =>
            WithResult<TResult>.Create(Execute(execute), Enabled(canExecute), state);

        private static Unit NoState => default;

        private static ExecuteAction Execute(Action<TParameter> execute) => new(execute);
        private static ExecuteAction<TState> Execute<TState>(Action<TState, TParameter> execute) => new(execute);

        private static Function<TResult> Execute<TResult>(Func<TParameter, TResult> execute) => new(execute);
        private static Function<TState, TResult> Execute<TState, TResult>(Func<TState, TParameter, TResult> execute) => new(execute);

        private static Function<bool> Enabled(Func<TParameter, bool> enabled) => new(enabled);
        private static Function<TState, bool> Enabled<TState>(Func<TState, TParameter, bool> enabled) => new(enabled);

        private static EnabledAlwaysFunction<Unit> EnabledAlways() => default;
        private static EnabledAlwaysFunction<TState> EnabledAlways<TState>() => default;

        private readonly struct ExecuteAction : IKineticFunction<Unit, TParameter, Unit>
        {
            public readonly Action<TParameter> Execute;
            public ExecuteAction(Action<TParameter> execute) => Execute = execute;

            public Unit Invoke(Unit state, TParameter parameter)
            {
                Execute(parameter);
                return default;
            }
        }

        private readonly struct ExecuteAction<TState> : IKineticFunction<TState, TParameter, Unit>
        {
            public readonly Action<TState, TParameter> Execute;
            public ExecuteAction(Action<TState, TParameter> execute) => Execute = execute;

            public Unit Invoke(TState state, TParameter parameter)
            {
                Execute(state, parameter);
                return default;
            }
        }

        private readonly struct Function<TResult> : IKineticFunction<Unit, TParameter, TResult>
        {
            public readonly Func<TParameter, TResult> Method;
            public Function(Func<TParameter, TResult> method) => Method = method;
            public TResult Invoke(Unit state, TParameter parameter) => Method(parameter);
        }

        private readonly struct Function<TState, TResult> : IKineticFunction<TState, TParameter, TResult>
        {
            public readonly Func<TState, TParameter, TResult> Method;
            public Function(Func<TState, TParameter, TResult> method) => Method = method;
            public TResult Invoke(TState state, TParameter parameter) => Method(state, parameter);
        }

        private readonly struct EnabledAlwaysFunction<TState> : IKineticFunction<TState, TParameter, bool>
        {
            public bool Invoke(TState state, TParameter parameter) => true;
        }

        private static KineticCommand<TParameter, Unit> Create<TExecute, TEnabled, TState>(TExecute execute, TEnabled enabled, TState state)
            where TExecute : struct, IKineticFunction<TState, TParameter, Unit>
            where TEnabled : struct, IKineticFunction<TState, TParameter, bool>
        {
            return new KineticCommand<KineticCommandHandler<TExecute, TEnabled, TState, TParameter, Unit>, TState, TParameter, Unit>(
                handler: new(execute, enabled), state);
        }

        private static KineticCommand<TParameter, Unit> Create<TExecute, TEnabled, TState>(TExecute execute, TEnabled enabled, IObservable<TState> state)
            where TExecute : struct, IKineticFunction<TState, TParameter, Unit>
            where TEnabled : struct, IKineticFunction<TState, TParameter, bool>
        {
            return new KineticCommand<KineticCommandHandler<TExecute, TEnabled, TState, TParameter, Unit>, TState, TParameter, Unit>(
                handler: new(execute, enabled), state);
        }

        private static class WithResult<TResult>
        {
            public static KineticCommand<TParameter, TResult> Create<TExecute, TEnabled, TState>(TExecute execute, TEnabled enabled, TState state)
                where TExecute : struct, IKineticFunction<TState, TParameter, TResult>
                where TEnabled : struct, IKineticFunction<TState, TParameter, bool>
            {
                return new KineticCommand<KineticCommandHandler<TExecute, TEnabled, TState, TParameter, TResult>, TState, TParameter, TResult>(
                    handler: new(execute, enabled), state);
            }
            
            public static KineticCommand<TParameter, TResult> Create<TExecute, TEnabled, TState>(TExecute execute, TEnabled enabled, IObservable<TState> state)
                where TExecute : struct, IKineticFunction<TState, TParameter, TResult>
                where TEnabled : struct, IKineticFunction<TState, TParameter, bool>
            {
                return new KineticCommand<KineticCommandHandler<TExecute, TEnabled, TState, TParameter, TResult>, TState, TParameter, TResult>(
                    handler: new(execute, enabled), state);
            }
        }
    }
}