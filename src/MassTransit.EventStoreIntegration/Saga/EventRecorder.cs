﻿using System;
using System.Collections;
using System.Collections.Generic;

namespace MassTransit.EventStoreIntegration.Saga
{
    public class EventRecorder : IEnumerable<object>
    {
        readonly List<object> _recorded;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventRecorder"/> class.
        /// </summary>
        public EventRecorder()
        {
            _recorded = new List<object>();
        }

        /// <summary>
        /// Records that the specified event happened.
        /// </summary>
        /// <param name="event">The event to record.</param>
        /// <exception cref="ArgumentNullException">Thrown when the specified <paramref name="event"/> is <c>null</c>.</exception>
        public void Record(object @event)
        {
            if (@event == null) throw new ArgumentNullException(nameof(@event));
            _recorded.Add(@event);
        }

        /// <summary>
        /// Resets this instance to its initial state.
        /// </summary>
        public void Reset() => _recorded.Clear();

        /// <summary>
        /// Gets an enumeration of recorded events.
        /// </summary>
        /// <returns>The recorded event enumerator.</returns>
        public IEnumerator<object> GetEnumerator() => _recorded.GetEnumerator();

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the collection.
        /// </returns>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}