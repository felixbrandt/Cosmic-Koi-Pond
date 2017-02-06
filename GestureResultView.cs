using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CosmicKoiPond
{
    /// <summary>
    /// Gesture Result viw for handling different gestures coming from Gesture Detector
    /// 1 GestureResultView for each body
    /// (mostly from Microsoft, Kinect examples)
    /// </summary>
    public sealed class GestureResultView : INotifyPropertyChanged
    {
        private int _bodyIndex;

        private float _confidence;

        private bool _detected;

        private bool _isTracked;

        private readonly VideoOutput _videoWindow;

        public GestureResultView(int bodyIndex, bool isTracked, bool detected, float confidence, VideoOutput videoWindow)
        {
            BodyIndex = bodyIndex;
            IsTracked = isTracked;
            Detected = detected;
            Confidence = confidence;
            _videoWindow = videoWindow;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public int BodyIndex
        {
            get { return _bodyIndex; }

            private set
            {
                if (_bodyIndex != value)
                {
                    _bodyIndex = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool IsTracked
        {
            get { return _isTracked; }

            private set
            {
                if (IsTracked != value)
                {
                    _isTracked = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool Detected
        {
            get { return _detected; }

            private set
            {
                if (_detected != value)
                {
                    _detected = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public float Confidence
        {
            get { return _confidence; }

            private set
            {
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (_confidence != value)
                {
                    _confidence = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public void UpdateGestureResult(string gestureName, bool isBodyTrackingIdValid, bool isGestureDetected, float detectionConfidence, ulong trackingId)
        {
            IsTracked = isBodyTrackingIdValid;
            Confidence = 0.0f;

            if (!IsTracked)
            {
                Detected = false;
            }
            else
            {
                Detected = isGestureDetected;

                if (Detected)
                {
                    Confidence = detectionConfidence;
                    // Gestures with confidence lower than 0.4 should be ignored
                    if (Confidence > 0.4)
                    {
                        // Handle different gestures coming from the database
                        switch (gestureName)
                        {
                            case "PushOut":
                                _videoWindow.CreateAnimatedCreature(CreatureType.Swarm, trackingId);
                                break;
                            case "WaveInwards_Left":
                                _videoWindow.CreateAnimatedCreature(CreatureType.Fish, trackingId, Direction.Right);
                                break;
                            case "WaveInwards_Right":
                                _videoWindow.CreateAnimatedCreature(CreatureType.Fish, trackingId, Direction.Left);
                                break;
                            case "WaveOutwards_Left":
                                _videoWindow.CreateAnimatedCreature(CreatureType.Fish, trackingId, Direction.Left);
                                break;
                            case "WaveOutwards_Right":
                                _videoWindow.CreateAnimatedCreature(CreatureType.Fish, trackingId, Direction.Right);
                                break;
                        }
                    }
                }
            }
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
