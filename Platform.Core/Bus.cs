﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Platform
{
    public abstract class Message
    {
    }

    internal interface IMessageHandler
    {
        string HandlerName { get; }
        bool TryHandle(Message message);
        bool IsSame(object handler);
    }

    public interface IHandle<T> where T : Message
    {
        void Handle(T message);
    }

    public interface ISubscriber
    {
        void AddHandler<T>(IHandle<T> handler) where T : Message;
        void Unsubscribe<T>(IHandle<T> handler) where T : Message;
    }
    public interface IPublisher
    {
        void Publish(Message message);
    }



    public interface IBus : IPublisher, ISubscriber
    {
        string Name { get; }
    }

    public sealed class MessageHandler<T> : IMessageHandler where T : Message
    {

        readonly IHandle<T> _handler;
        public MessageHandler(IHandle<T> handler, string handlerName)
        {
            Contract.Requires(handler != null);
            HandlerName = handlerName ?? "";
            _handler = handler;
        }

        public string HandlerName { get; private set; }
        public bool TryHandle(Message message)
        {
            var msg = message as T;

            if (msg != null)
            {
                _handler.Handle(msg);
                return true;
            }
            return false;
        }

        public bool IsSame(object handler)
        {
            return ReferenceEquals(_handler, handler);
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(HandlerName) ? _handler.ToString() : HandlerName;
        }
    }

    public class PublishEnvelope : IEnvelope
    {
        private readonly IPublisher _publisher;

        public PublishEnvelope(IPublisher publisher)
        {
            _publisher = publisher;
        }

        public void ReplyWith<T>(T message) where T : Message
        {
            _publisher.Publish(message);
        }
    }



    public interface IEnvelope
    {
        void ReplyWith<T>(T message) where T : Message;
    }

    public sealed class InMemoryBus : IBus, IPublisher, ISubscriber, IHandle<Message>
    {

        private readonly Dictionary<Type, List<IMessageHandler>> _typeLookup = new Dictionary<Type, List<IMessageHandler>>();

        public void AddHandler<T>(IHandle<T> handler) where T : Message
        {
            Contract.Requires(handler != null);

            List<IMessageHandler> handlers;
            var type = typeof(T);
            if (!_typeLookup.TryGetValue(type, out handlers))
            {
                _typeLookup.Add(type, handlers = new List<IMessageHandler>());
            }
            if (!handlers.Any(h => h.IsSame(handler)))
            {
                handlers.Add(new MessageHandler<T>(handler, handler.GetType().Name));
            }
        }

        public void Unsubscribe<T>(IHandle<T> handler) where T : Message
        {
            Contract.Requires(handler != null);
            List<IMessageHandler> list;
            if (_typeLookup.TryGetValue(typeof(T), out list))
            {
                list.RemoveAll(x => x.IsSame(handler));
            }
        }

        public string Name { get; private set; }

        public InMemoryBus(string name)
        {
            Name = name;
        }

        public void Publish(Message message)
        {
            Contract.Requires(message != null);
            DispatchByType(message);
        }

        public void Handle(Message message)
        {
            Contract.Requires(message != null);
            DispatchByType(message);
        }

        void DispatchByType(Message message)
        {
            var type = message.GetType();
            do
            {
                DispatchByType(message, type);
                type = type.BaseType;
            } while (type != typeof(Message));
        }

        void DispatchByType(Message message, Type type)
        {
            List<IMessageHandler> list;
            if (!_typeLookup.TryGetValue(type, out list)) return;
            foreach (var handler in list)
            {
                handler.TryHandle(message);
            }
        }
    }


    public static class HandleExtensions
    {
        /// <summary>
        /// Narrows down scope of message handler handler from <typeparamref name="TOutput"/>
        /// to <typeparamref name="TInput"/>
        /// </summary>
        public static IHandle<TInput> WidenFrom<TInput, TOutput>(this IHandle<TOutput> handler)
            where TOutput : Message
            where TInput : TOutput
        {
            return new WideningHandler<TInput, TOutput>(handler);
        }
    }

    public class WideningHandler<TInput, TOutput> : IHandle<TInput>
        where TInput : TOutput
        where TOutput : Message
    {
        private readonly IHandle<TOutput> _handler;

        public WideningHandler(IHandle<TOutput> handler)
        {
            _handler = handler;
        }

        public void Handle(TInput message)
        {
            _handler.Handle(message);
        }
    }
    public sealed class QueuedHandler : IHandle<Message>, IPublisher
    {
        readonly IHandle<Message> _consumer;
        readonly ConcurrentQueue<Message> _queue = new ConcurrentQueue<Message>();
        private static readonly ILogger Log = LogManager.GetLoggerFor<QueuedHandler>();
        Task _thread;

        readonly int _waitToStopThreadMs;

        readonly string _name;
        readonly CancellationTokenSource _cancel = new CancellationTokenSource();


        public QueuedHandler(IHandle<Message> consumer, string name, int waitToStopThreadMs = 10000)
        {
            _consumer = consumer;
            _name = name;
            _waitToStopThreadMs = waitToStopThreadMs;
        }

        public void Start()
        {
            if (null != _thread)
                throw new InvalidOperationException("Thread is already running");


            _thread = Task.Factory.StartNew(() => ReadMessagesFromQueue(_cancel.Token), TaskCreationOptions.LongRunning);

        }

        public void Stop()
        {
            _cancel.Cancel();
            if (null == _thread) return;
            if (_thread.Wait(_waitToStopThreadMs))
            {
                throw new InvalidOperationException("Failed to stop thread ");
            }

        }

        void IHandle<Message>.Handle(Message message)
        {
            Contract.Requires(message != null);
            _queue.Enqueue(message);
        }

        void ReadMessagesFromQueue(CancellationToken token)
        {
            Thread.CurrentThread.Name = _name;

            while (!token.IsCancellationRequested)
            {
                Message result;

                if (_queue.TryDequeue(out result))
                {
                    try
                    {
                        _consumer.Handle(result);
                    }
                    catch (Exception ex)
                    {
                        Log.ErrorException(ex, "Error while processing message {0} in queued handler '{1}'.", result, _name);
                    }
                }
                else
                {
                    token.WaitHandle.WaitOne(1);
                }
            }
        }

        void IPublisher.Publish(Message message)
        {
            Contract.Requires(message != null);
            _queue.Enqueue(message);
        }

        public void Enqueue(Message message)
        {
            _queue.Enqueue(message);
        }
    }

}
