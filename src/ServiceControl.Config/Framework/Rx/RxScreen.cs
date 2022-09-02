namespace ServiceControl.Config.Framework.Rx
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Caliburn.Micro;

    public class RxScreen : RxViewAware, IScreen, IChild, IModalResult
    {
        /// <summary>
        /// Indicates whether or not this instance is currently initialized.
        /// </summary>
        public bool IsInitialized { get; private set; }


        /// <summary>
        /// Gets or Sets the Parent <see cref="IConductor" />
        /// </summary>
        public virtual object Parent { get; set; }

        /// <summary>
        /// Gets or Sets the Modal Result
        /// </summary>
        public virtual bool? Result { get; set; }

        /// <summary>
        /// Gets or Sets the Display Name
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Indicates whether or not this instance is currently active.
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// Raised after activation occurs.
        /// </summary>
        public event EventHandler<ActivationEventArgs> Activated = (sender, e) => { };

        public IEventAggregator EventAggregator { get; set; }

        /// <summary>
        /// Raised before deactivation.
        /// </summary>
        public event EventHandler<DeactivationEventArgs> AttemptingDeactivation = (sender, e) => { };

        /// <summary>
        /// Raised after deactivation.
        /// </summary>
        public event AsyncEventHandler<DeactivationEventArgs> Deactivated = (sender, e) => Task.CompletedTask;

        async Task IActivate.ActivateAsync(CancellationToken cancellationToken)
        {
            if (IsActive)
            {
                return;
            }

            var initialized = false;

            if (!IsInitialized)
            {
                IsInitialized = initialized = true;
                await OnInitialize();
            }

            IsActive = true;
            Log.Info("Activating {0}.", this);
            await OnActivate();

            Activated(this, new ActivationEventArgs
            {
                WasInitialized = initialized
            });
        }

        async Task IDeactivate.DeactivateAsync(bool close, CancellationToken cancellationToken)
        {
            if (IsActive || (IsInitialized && close))
            {
                AttemptingDeactivation(this, new DeactivationEventArgs
                {
                    WasClosed = close
                });

                IsActive = false;
                Log.Info("Deactivating {0}.", this);
                await OnDeactivate(close);

                await Deactivated(this, new DeactivationEventArgs
                {
                    WasClosed = close
                });

                if (close)
                {
                    Views.Clear();
                    Log.Info("Closed {0}.", this);
                }
            }
        }

        /// <summary>
        /// Called to check whether or not this instance can close.
        /// </summary>
        /// <param name="callback">The implementor calls this action with the result of the close check.</param>
        public virtual Task<bool> CanCloseAsync(CancellationToken cancellationToken) => Task.FromResult(true);

        /// <summary>
        /// Tries to close this instance by asking its Parent to initiate shutdown or by asking its corresponding view to close.
        /// Also provides an opportunity to pass a dialog result to it's corresponding view.
        /// </summary>
        /// <param name="dialogResult">The dialog result.</param>
        public virtual Task TryCloseAsync(bool? dialogResult = null)
        {
            Result = dialogResult;

            if (IsHandler(this))
            {
                EventAggregator?.Unsubscribe(this);
            }

            if (Parent is IConductor conductor)
            {
                return conductor.CloseItemAsync(this);
            }

            var closeAction = PlatformProvider.Current.GetViewCloseAction(this, Views.Values, dialogResult);
            return closeAction.Invoke(CancellationToken.None);
        }

        static bool IsHandler(object obj)
        {
            var interfaces = obj.GetType().GetInterfaces()
                .Where(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IHandle<>));
            return interfaces.Any();
        }

        /// <summary>
        /// Called when initializing.
        /// </summary>
        protected virtual Task OnInitialize() => Task.CompletedTask;

        /// <summary>
        /// Called when activating.
        /// </summary>
        protected virtual Task OnActivate() => Task.CompletedTask;

        /// <summary>
        /// Called when deactivating.
        /// </summary>
        /// <param name="close">Inidicates whether this instance will be closed.</param>
        protected virtual Task OnDeactivate(bool close) => Task.CompletedTask;

        static readonly ILog Log = LogManager.GetLog(typeof(Screen));
    }
}