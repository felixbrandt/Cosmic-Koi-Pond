using System;
using System.Collections.Generic;
using Microsoft.Kinect;
using Microsoft.Kinect.VisualGestureBuilder;

namespace CosmicKoiPond
{
    /// <summary>
    /// Gesture Detector class which listens for VisualGestureBuilderFrame events from the service
    /// and updates the associated GestureResultView object with the latest results for the 'Seated' gesture
    /// (mostly from Microsoft, Kinect examples)
    /// </summary>
    public class GestureDetector : IDisposable
    {
        private readonly string gestureDatabase = @"GestureDatabase\CosmicKoiPond.gbd";

        private VisualGestureBuilderFrameSource _vgbFrameSource;

        private VisualGestureBuilderFrameReader _vgbFrameReader;

        public GestureDetector(KinectSensor kinectSensor, GestureResultView gestureResultView)
        {
            if (kinectSensor == null)
            {
                throw new ArgumentNullException(nameof(kinectSensor));
            }

            if (gestureResultView == null)
            {
                throw new ArgumentNullException(nameof(gestureResultView));
            }

            GestureResultView = gestureResultView;

            _vgbFrameSource = new VisualGestureBuilderFrameSource(kinectSensor, 0);
            _vgbFrameSource.TrackingIdLost += Source_TrackingIdLost;

            _vgbFrameReader = _vgbFrameSource.OpenReader();
            if (_vgbFrameReader != null)
            {
                _vgbFrameReader.IsPaused = true;
                _vgbFrameReader.FrameArrived += Reader_GestureFrameArrived;
            }

            using (VisualGestureBuilderDatabase database = new VisualGestureBuilderDatabase(gestureDatabase))
            {
                foreach (Gesture gesture in database.AvailableGestures)
                {
                    // Add all gestures from Database
                    _vgbFrameSource.AddGesture(gesture);
                }
            }
        }

        public GestureResultView GestureResultView { get; }

        internal ulong TrackingId
        {
            get { return _vgbFrameSource.TrackingId; }
            set { _vgbFrameSource.TrackingId = value; }
        }

        public bool IsPaused
        {
            get { return _vgbFrameReader.IsPaused; }

            set
            {
                if (_vgbFrameReader.IsPaused != value)
                {
                    _vgbFrameReader.IsPaused = value;
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_vgbFrameReader != null)
                {
                    _vgbFrameReader.FrameArrived -= Reader_GestureFrameArrived;
                    _vgbFrameReader.Dispose();
                    _vgbFrameReader = null;
                }

                if (_vgbFrameSource != null)
                {
                    _vgbFrameSource.TrackingIdLost -= Source_TrackingIdLost;
                    _vgbFrameSource.Dispose();
                    _vgbFrameSource = null;
                }
            }
        }

        /// <summary>
        /// Gets called when a new gesture frame arrives
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">Event</param>
        private void Reader_GestureFrameArrived(object sender, VisualGestureBuilderFrameArrivedEventArgs e)
        {
            VisualGestureBuilderFrameReference frameReference = e.FrameReference;
            using (VisualGestureBuilderFrame frame = frameReference.AcquireFrame())
            {
                var discreteResults = frame?.DiscreteGestureResults;

                if (discreteResults == null) return;

                foreach (Gesture gesture in _vgbFrameSource.Gestures)
                {
                    if (gesture.GestureType != GestureType.Discrete) continue;
                    DiscreteGestureResult result;
                    discreteResults.TryGetValue(gesture, out result);

                    if (result != null)
                    {
                        GestureResultView.UpdateGestureResult(gesture.Name, true, result.Detected, result.Confidence, TrackingId);
                    }
                }
            }
        }

        private void Source_TrackingIdLost(object sender, TrackingIdLostEventArgs e)
        {
            GestureResultView.UpdateGestureResult("", false, false, 0.0f, 0);
        }
    }
}
