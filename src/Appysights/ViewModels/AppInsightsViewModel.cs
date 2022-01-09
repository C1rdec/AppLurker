﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using Appysights.Models;
using Appysights.Services;
using MahApps.Metro.Controls;

namespace Appysights.ViewModels
{
    public class AppInsightsViewModel : Caliburn.Micro.PropertyChangedBase, System.IDisposable
    {
        #region Fields

        private AppInsightsService _service;
        private IEnumerable<AppInsightEvent> _currentEvents;
        private Position _position;
        private int _pageIndex = 0;
        private int _pageSize = 20;
        private readonly object pageLock = new();
        private bool _listenScroll = true;
        private bool _requestMode;

        #endregion

        #region Constructors

        public AppInsightsViewModel(AppInsightsService service, Position position)
        {
            _position = position;
            _service = service;
            _service.NewEvent += Service_NewEvent;
            Events = new ObservableCollection<EventTileViewModel>();

            _currentEvents = _service.Events;
            DisplayNextPage();
        }

        #endregion

        #region Properties

        public ObservableCollection<EventTileViewModel> Events { get; set; }

        public string ServiceName => _service.Name;

        public bool Selected => Events.Any(e => e.Selected);

        public bool RequestMode
        {
            get
            {
                return _requestMode;
            }

            private set
            {
                _requestMode = value;
                NotifyOfPropertyChange();
            }
        }

        #endregion

        #region Methods

        public async void ToggleEvents()
        {
            Clear();
            if (!RequestMode)
            {
                RequestMode = true;
                _currentEvents = await _service.GetLastHourFailedRequests();
            }
            else
            {
                RequestMode = false;
                _currentEvents = _service.Events;
            }


            DisplayNextPage();
        }

        public async void RefreshRequests()
        {
            Clear();
            _currentEvents = await _service.GetLastHourFailedRequests();
            DisplayNextPage();
        }

        public void OnScroll(System.Windows.Controls.ScrollChangedEventArgs scrollEvent)
        {
            if (!_listenScroll)
            {
                _listenScroll = true;
                return;
            }

            var position = scrollEvent.VerticalOffset + scrollEvent.ViewportHeight;
            var heightThreshold = scrollEvent.ExtentHeight / 1.1;

            if (position >= heightThreshold)
            {
                if (Monitor.TryEnter(pageLock))
                {
                    try
                    {
                        DisplayNextPage();
                    }
                    finally
                    {
                        Monitor.Exit(pageLock);
                    }
                }
            }
        }

        public void Next()
        {
            var selectedEvent = Events.FirstOrDefault(e => e.Selected);
            if (selectedEvent == null)
            {
                var first = Events.FirstOrDefault();
                if (first != null)
                {
                    first.Select();
                }
            }
            else
            {
                var index = Events.IndexOf(selectedEvent);
                if (index == -1 || index + 1 >= Events.Count)
                {
                    return;
                }

                Events[index + 1].Select();
            }
        }

        public void Previous()
        {
            var selectedEvent = Events.FirstOrDefault(e => e.Selected);
            if (selectedEvent == null)
            {
                return;
            }

            var index = Events.IndexOf(selectedEvent);
            if (index == -1 || index - 1 < 0)
            {
                return;
            }

            Events[index - 1].Select();
        }

        public void Dispose()
        {
            _service.NewEvent -= Service_NewEvent;
        }

        private void Service_NewEvent(object sender, AppInsightEvent e)
        {
            Events.Add(new EventTileViewModel(e, _position));
        }

        private void DisplayNextPage()
        {
            if (Events.Count >= _currentEvents.Count())
            {
                return;
            }

            var eventToDisplay = _currentEvents.Skip(_pageIndex * _pageSize).Take(_pageSize);
            foreach (var appInsightEvent in eventToDisplay)
            {
                Events.Add(new EventTileViewModel(appInsightEvent, _position));
            }

            _pageIndex++;
        }

        private void Clear()
        {
            _listenScroll = false;
            Events.Clear();

            _pageIndex = 0;
            //NotifyOfPropertyChange(() => Events);
        }

        #endregion
    }
}
