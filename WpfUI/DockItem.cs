using System;
using System.ComponentModel;
using System.Windows;

namespace DockPanelSuite.Wpf
{
    public enum DockState { Document, Left, Right, Top, Bottom, Float, Hidden }

    /// <summary>A persistent identity and content instance owned by one workspace.</summary>
    public sealed class DockItem : INotifyPropertyChanged
    {
        private string _title;
        private DockState _state = DockState.Hidden;
        private bool _canClose = true;

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
        public DockState State => _state;
        public DockState LastDockState { get; internal set; } = DockState.Document;
        public Rect FloatingBounds { get; internal set; } = new Rect(100, 100, 640, 400);
        internal DockWorkspace Owner { get; set; }

        internal void SetState(DockState state)
        {
            _state = state;
            if (state != DockState.Hidden && state != DockState.Float) LastDockState = state;
            Notify(nameof(State));
        }
        public event PropertyChangedEventHandler PropertyChanged;
        private void Notify(string property) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }
}
