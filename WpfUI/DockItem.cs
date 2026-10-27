using System;
using System.ComponentModel;
using System.Windows;

namespace DockPanelSuite.Wpf
{
    /// <summary>A persistent identity and content instance owned by one workspace.</summary>
    public sealed class DockItem : INotifyPropertyChanged
    {
        private string _title;
        private DockState _state = DockState.Hidden;
        private bool _canClose = true;
        private bool _showCloseButton = true;
        private bool _isActive;
        private bool _canAutoHide = true;
        private bool _canDockToSides = true;

        public DockItem(string id, string title, object content)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("An ID is required.", nameof(id));
            Id = id;
            _title = title ?? throw new ArgumentNullException(nameof(title));
            Content = content ?? throw new ArgumentNullException(nameof(content));
        }

        public string Id { get; }
        public object Content { get; }
        public string Title
        {
            get => _title;
            set { Owner?.Dispatcher.VerifyAccess(); _title = value ?? throw new ArgumentNullException(nameof(value)); Notify(nameof(Title)); }
        }
        public bool CanClose
        {
            get => _canClose;
            set { Owner?.Dispatcher.VerifyAccess(); _canClose = value; Notify(nameof(CanClose)); }
        }
        /// <summary>Controls close chrome; CanClose also guards commands and native closing.</summary>
        public bool ShowCloseButton
        {
            get => _showCloseButton;
            set { Owner?.Dispatcher.VerifyAccess(); _showCloseButton = value; Notify(nameof(ShowCloseButton)); }
        }
        /// <summary>Whether this item may be collapsed into an auto-hide rail.</summary>
        public bool CanAutoHide
        {
            get => _canAutoHide;
            set { Owner?.Dispatcher.VerifyAccess(); _canAutoHide = value; Notify(nameof(CanAutoHide)); }
        }
        /// <summary>Whether this item may dock at workspace edges or split a target on a side.</summary>
        public bool CanDockToSides
        {
            get => _canDockToSides;
            set { Owner?.Dispatcher.VerifyAccess(); _canDockToSides = value; Notify(nameof(CanDockToSides)); }
        }
        internal DockState DocumentAutoHideEdge { get; set; } = DockState.Bottom;
        public DockState AutoHideEdge => LastDockState == DockState.Document ? DocumentAutoHideEdge : LastDockState;

        /// <summary>Whether this item is the workspace's active pane, independently of its group's selected tab.</summary>
        public bool IsActive => _isActive;
        public DockState State => _state;
        public DockState LastDockState { get; internal set; } = DockState.Document;
        public Rect FloatingBounds { get; internal set; } = new Rect(100, 100, 640, 400);
        internal DockWorkspace Owner { get; set; }

        internal void SetActive(bool value)
        {
            if (_isActive == value) return;
            _isActive = value;
            Notify(nameof(IsActive));
        }

        internal void SetState(DockState state)
        {
            _state = state;
            if (state != DockState.Hidden && state != DockState.Float && state != DockState.AutoHide) LastDockState = state;
            Notify(nameof(State));
        }
        public event PropertyChangedEventHandler PropertyChanged;
        private void Notify(string property) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }
}
