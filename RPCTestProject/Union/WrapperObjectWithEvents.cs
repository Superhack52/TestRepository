using System;
using System.Collections.Generic;

namespace Union
{
    internal class EventEmitter
    {
        public event Action<dynamic> Subject;

        public void Emit(object value) => Subject?.Invoke(value);
    }

    internal class EventItem
    {
        internal Guid EventKey;
        internal EventEmitter Event;

        internal EventItem(Guid eventKey, EventEmitter @event)
        {
            EventKey = eventKey;
            Event = @event;
        }
    }

    /// <summary>
    /// Словарь имен события и EventKey с EventEmitter
    /// </summary>
    internal class WrapperObjectWithEvents
    {
        public WrapperObjectWithEvents(dynamic target, TcpConnector connector)
        {
            _target = target;
            _connector = connector;
        }

        /// <summary>
        /// Вызывается при получении внешнего события из .Net
        /// </summary>
        /// <param name="eventKey"></param>
        /// <param name="value"></param>
        public void RaiseEvent(Guid eventKey, object value)
        {
            // Если есть подписчики, то вызываем их
            EventEmitter @event;
            if (_eventEmittersList.TryGetValue(eventKey, out @event)) @event.Emit(value);
        }

        //public void AddEventHandler(string eventName, Action<dynamic> eventHandler)
        //{
        //    var isFirst = false;

        //    if (!this._eventsList.TryGetValue(eventName, out var eventItem))
        //    {
        //        var eventKey = Guid.NewGuid();
        //        var @event = new EventEmitter();
        //        eventItem = new EventItem(eventKey, @event);
        //        _eventsList.Add(eventName, eventItem);
        //        _eventEmittersList.Add(eventKey, @event);
        //        _connector.EventDictionary.Add(eventKey, this);
        //        isFirst = true;
        //    }

        //    eventItem.Event.Subject += eventHandler;
        //    if (isFirst) _target.AddEventHandler(eventItem.EventKey, eventName);
        //}

        //public void Close()
        //{
        //    this.RemoveAllEventHandler();
        //}

        //public void RemoveAllEventHandler()
        //{
        //    foreach (var ei in _eventsList.Values) _connector.EventDictionary.Remove(ei.EventKey);

        //    _eventsList.Clear();
        //    _eventEmittersList.Clear();
        //}

        private dynamic _target;
        private TcpConnector _connector;

        // Словарь EventKey и EventEmitter
        private Dictionary<Guid, EventEmitter> _eventEmittersList = new Dictionary<Guid, EventEmitter>();

        private Dictionary<string, EventItem> _eventsList = new Dictionary<string, EventItem>();
    }
}