using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PubSub
{
    public class Hub
    {
        internal class Handler
        {
            public Delegate Action { get; set; }
            public WeakReference Sender { get; set; }
            public string RoutingKey { get; set; }
            public Type Type { get; set; }
        }

        internal object locker = new object();
        internal List<Handler> handlers = new List<Handler>();

        /// <summary>
        /// Allow publishing directly onto this Hub.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="routingKey"></param>
        /// <param name="data"></param>
        public void Publish<T>(string routingKey = "", T data = default(T))
        {
            Publish(this, routingKey, data);
        }

        public void Publish<T>(object sender, string routingKey = "", T data = default(T))
        {
            var handlerList = new List<Handler>(handlers.Count);
            var handlersToRemoveList = new List<Handler>(handlers.Count);

            lock (this.locker)
            {
                foreach (var handler in handlers)
                {
                    if (!handler.Sender.IsAlive)
                    {
                        handlersToRemoveList.Add(handler);
                    }
                    else if (handler.Type.IsAssignableFrom(typeof(T)))
                    {
                        handlerList.Add(handler);
                    }
                }

                foreach (var l in handlersToRemoveList)
                {
                    handlers.Remove(l);
                }
            }

            foreach (var l in handlerList)
            {
                if (!string.IsNullOrEmpty(routingKey))
                {
                    if (Match(l.RoutingKey, routingKey))
                    {
                        ((Action<T>)l.Action)(data);
                    }
                }
                else
                {
                    ((Action<T>)l.Action)(data);
                }

            }
        }

        public bool Match(string actual, string expected)
        {
            if (actual == expected) return true;
            var regexString = '^' + expected.Replace("*", "([^.]+)").Replace("#", @"([^.]+\.?)+") + '$';
            return Regex.Match(actual, regexString).Success;
        }

        /// <summary>
        /// Allow subscribing directly to this Hub.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handler"></param>
        /// <param name="routingKey"></param>
        public void Subscribe<T>(Action<T> handler, string routingKey = "")
        {
            Subscribe(this, handler, routingKey);
        }

        public void Subscribe<T>(object sender, Action<T> handler, string routingKey = "")
        {
            var item = new Handler
            {
                Action = handler,
                Sender = new WeakReference(sender),
                Type = typeof(T),
                RoutingKey = routingKey

            };

            lock (this.locker)
            {
                this.handlers.Add(item);
            }
        }

        /// <summary>
        /// Allow unsubscribing directly to this Hub.
        /// </summary>
        public void Unsubscribe()
        {
            Unsubscribe(this);
        }

        public void Unsubscribe(object sender)
        {
            lock (this.locker)
            {
                var query = this.handlers.Where(a => !a.Sender.IsAlive ||
                                                     a.Sender.Target.Equals(sender));

                foreach (var h in query.ToList())
                {
                    this.handlers.Remove(h);
                }
            }
        }

        /// <summary>
        /// Allow unsubscribing directly to this Hub.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void Unsubscribe<T>()
        {
            Unsubscribe<T>(this);
        }

        /// <summary>
        /// Allow unsubscribing directly to this Hub.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handler"></param>
        public void Unsubscribe<T>(Action<T> handler = null)
        {
            Unsubscribe<T>(this, handler);
        }

        public void Unsubscribe<T>(object sender, Action<T> handler = null)
        {
            lock (this.locker)
            {
                var query = this.handlers
                    .Where(a => !a.Sender.IsAlive ||
                                (a.Sender.Target.Equals(sender) && a.Type == typeof(T)));

                if (handler != null)
                {
                    query = query.Where(a => a.Action.Equals(handler));
                }

                foreach (var h in query.ToList())
                {
                    this.handlers.Remove(h);
                }
            }
        }
    }
}