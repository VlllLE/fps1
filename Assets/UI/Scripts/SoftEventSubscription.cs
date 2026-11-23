using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace CodMovementShooter.UI
{
    /// <summary>
    /// Subscribes to CLR events by name without a compile-time dependency on the declaring type.
    /// Used so the HUD assembly never hard-references Scripts (WeaponController / HitDetection).
    /// </summary>
    public sealed class SoftEventSubscription : IDisposable
    {
        private readonly List<(object target, EventInfo evt, Delegate handler)> _bindings =
            new List<(object, EventInfo, Delegate)>();

        public bool TrySubscribe(object target, string eventName, Action<object[]> handler)
        {
            if (target == null || string.IsNullOrEmpty(eventName) || handler == null)
                return false;

            EventInfo evt = target.GetType().GetEvent(eventName, BindingFlags.Instance | BindingFlags.Public);
            if (evt == null || evt.EventHandlerType == null)
                return false;

            Delegate relay = BuildRelay(evt.EventHandlerType, handler);
            if (relay == null)
                return false;

            evt.AddEventHandler(target, relay);
            _bindings.Add((target, evt, relay));
            return true;
        }

        public void Dispose()
        {
            for (int i = 0; i < _bindings.Count; i++)
            {
                (object target, EventInfo evt, Delegate handler) = _bindings[i];
                try
                {
                    if (target != null)
                        evt.RemoveEventHandler(target, handler);
                }
                catch
                {
                    // Target may already be destroyed.
                }
            }

            _bindings.Clear();
        }

        private static Delegate BuildRelay(Type handlerType, Action<object[]> sink)
        {
            MethodInfo invoke = handlerType.GetMethod("Invoke");
            if (invoke == null)
                return null;

            ParameterInfo[] parameters = invoke.GetParameters();
            ParameterExpression[] paramExprs = new ParameterExpression[parameters.Length];
            Expression[] boxed = new Expression[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                paramExprs[i] = Expression.Parameter(parameters[i].ParameterType, "a" + i);
                boxed[i] = Expression.Convert(paramExprs[i], typeof(object));
            }

            Expression array = Expression.NewArrayInit(typeof(object), boxed);
            Expression body = Expression.Invoke(Expression.Constant(sink), array);
            LambdaExpression lambda = Expression.Lambda(handlerType, body, paramExprs);
            return lambda.Compile();
        }
    }
}
