using System;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using XamlAnimatedGif;
using System.Windows;

namespace CosmicKoiPond
{
    /// <summary>
    /// Lilypad that gets displayed on the canvas.
    /// Indicates the presence of a skeleton visible to the Kinect.
    /// Multiple bodies mean multiple lilypads.
    /// Has a spawn and despawn animation, that gets played automatically when creating the object.
    /// </summary>
    internal class Lilypad
    {
        // List of possible sounds for lilypads
        private static readonly Uri SoundUri = new Uri("Sound/lilypad.wav", UriKind.Relative);

        public readonly float Scale;

        // Image URIs
        public readonly Uri SpawnUri;
        public readonly Uri CycleUri;
        public readonly Uri DespawnUri;

        // Time when the last gesture was detected by the user of this lilypad
        private DateTime _lastGestureDetected = DateTime.Now;

        // Images
        public Image SpawnImage;
        public Image CycleImage;
        public Image DespawnImage;

        // Animation storyboard
        public readonly Storyboard Storyboard = new Storyboard();

        // Helpers
        private bool _spawned;
        private bool _waitingForDestroy;

        public VideoOutput VideoWindow { get; }

        public Lilypad(float scale, Uri spawnUri, Uri cycleUri, Uri despawnUri, VideoOutput videoWindow)
        {
            // Do a bit of variation on the scale (+-30%)
            Random random = new Random();
            float scalingFactor = random.Next(70, 130);
            scalingFactor /= 100; // now it's 0.7 to 1.29 (good enough)
            Scale = scale * scalingFactor;

            // Set image Uris (handled by VideoOutput)
            SpawnUri = spawnUri;
            CycleUri = cycleUri;
            DespawnUri = despawnUri;

            // VideoOutput
            VideoWindow = videoWindow;

            // Spawn Sound
            if (VideoWindow.State == MovieState.Playing)
            {
                MediaElement mediaElement = new MediaElement
                {
                    UnloadedBehavior = MediaState.Close,
                    Source = SoundUri
                };
                VideoWindow.Body.Children.Add(mediaElement);
                mediaElement.MediaEnded += MediaElementOnMediaEnded;
            }
            
            Spawn();
        }

        /// <summary>
        /// Remove sound media element once the sound has finished playing
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">Event</param>
        private void MediaElementOnMediaEnded(object sender, RoutedEventArgs e)
        {
            // Remove MediaPlayer from VideoWindow so it will be disposed
            VideoWindow.Body.Children.Remove((MediaElement)sender);
        }

        /// <summary>
        /// Spawning logic for lilypads.
        /// Spawns lilypads on the canvas of VideoWindow
        /// </summary>
        private void Spawn()
        {
            // Reference image for getting dimensions
            BitmapImage refImage = new BitmapImage(SpawnUri);

            // Create images
            CycleImage = new Image();
            SpawnImage = new Image();
            DespawnImage = new Image
            {
                Width = refImage.Width*Scale,
                Height = refImage.Height*Scale
            };

            // Set dimensions for images
            CycleImage.Width = refImage.Width * Scale;
            CycleImage.Height = refImage.Height * Scale;
            SpawnImage.Width = refImage.Width * Scale;
            SpawnImage.Height = refImage.Height * Scale;

            // Add animation behaviour for DespawnImage
            AnimationBehavior.SetAutoStart(DespawnImage, false);
            AnimationBehavior.SetSourceUri(DespawnImage, DespawnUri);
            AnimationBehavior.SetRepeatBehavior(DespawnImage, new RepeatBehavior(1));
            AnimationBehavior.AddLoadedHandler(DespawnImage, (sender, args) => AnimationBehavior.GetAnimator(sender as Image).AnimationCompleted += Destroy);

            // Add animation behaviour for CycleImage
            AnimationBehavior.SetAutoStart(CycleImage, false);
            AnimationBehavior.SetSourceUri(CycleImage, CycleUri);
            AnimationBehavior.SetRepeatBehavior(CycleImage, RepeatBehavior.Forever);

            // Add animation behaviour for SpawnImage
            AnimationBehavior.SetAutoStart(SpawnImage, true);
            AnimationBehavior.SetSourceUri(SpawnImage, SpawnUri);
            AnimationBehavior.SetRepeatBehavior(SpawnImage, new RepeatBehavior(1));
            AnimationBehavior.AddLoadedHandler(SpawnImage, (sender, args) => AnimationBehavior.GetAnimator(sender as Image).AnimationCompleted += SpawnFinished);

            // Calculate where the creature is spawning (20% border padding)
            Random random = new Random();

            // 20% padding on each side
            int leftOffset = random.Next((int)(VideoWindow.ActualWidth / 5), (int)(VideoWindow.ActualWidth - SpawnImage.ActualWidth*1.5 - (int)(VideoWindow.ActualWidth / 5)));
            int topOffset = random.Next((int)(VideoWindow.ActualHeight / 5), (int)(VideoWindow.ActualHeight - SpawnImage.ActualHeight*1.5 - (int)(VideoWindow.ActualHeight / 5)));

            // Set positions for the images to the generated position
            Canvas.SetTop(DespawnImage, topOffset);
            Canvas.SetTop(SpawnImage, topOffset);
            Canvas.SetTop(CycleImage, topOffset);
            Canvas.SetLeft(DespawnImage, leftOffset);
            Canvas.SetLeft(SpawnImage, leftOffset);
            Canvas.SetLeft(CycleImage, leftOffset);

            // Add the images to the canvas
            DespawnImage.Opacity = 0; // Don't show DespawnImage at startup
            VideoWindow.LillypadCanvas.Children.Add(DespawnImage);
            CycleImage.Opacity = 0; // Don't show CycleImage at startup
            VideoWindow.LillypadCanvas.Children.Add(CycleImage);
            VideoWindow.LillypadCanvas.Children.Add(SpawnImage);
        }

        /// <summary>
        /// Gets called when the spawn animation has finished playing
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">Event</param>
        private void SpawnFinished(object sender, EventArgs e)
        {
            _spawned = true;

            if (_waitingForDestroy)
            {
                // The player has left the frame during the spawn animation
                // -> Play despawn animation
                AnimationBehavior.GetAnimator(DespawnImage).Play();
                DespawnImage.Opacity = 1;
            }
            else
            {
                // The player is still in frame
                // -> Play cycle animation
                AnimationBehavior.GetAnimator(CycleImage).Play();
                CycleImage.Opacity = 1;
            }

            // Remove spawn image from canvas
            VideoWindow.LillypadCanvas.Children.Remove(SpawnImage);
        }

        /// <summary>
        /// Gets called when despawning is finished to clean up
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e"></param>
        private void Destroy(object sender, EventArgs e)
        {
            AnimationBehavior.GetAnimator(DespawnImage).Pause();  // pause the animation of the despawn image
            VideoWindow.LillypadCanvas.Children.Remove(DespawnImage);
            Storyboard.Children.Clear(); // remove all animations from storyboard (movement + rotation)
            Storyboard.Stop(); // stop the storyboard
        }

        /// <summary>
        /// Despawn the lilypad. Gets called when a player leaves the frame
        /// </summary>
        public void Despawn()
        {
            if (_spawned)
            {
                // Lilypad has finished spawning and can be despawned
                DespawnImage.Opacity = 1;
                VideoWindow.LillypadCanvas.Children.Remove(CycleImage);
                AnimationBehavior.GetAnimator(DespawnImage).Play();
            } else
            {
                // Lilypad is still spawning, will despawn once spawning is finished
                _waitingForDestroy = true;
            }   
        }

        /// <summary>
        /// Add visual feedback when recognizing a gesture.
        /// Makes the lilypad pulse
        /// </summary>
        internal void TriggerGesture()
        {
            // Have 2 seconds between animations so it does not get too crowded
            if (_lastGestureDetected < DateTime.Now.Subtract(new TimeSpan(0, 0, 2)))
            {
                _lastGestureDetected = DateTime.Now;

                // Create animation that changes the size to 110% and back to 100% (1 second duration * 2)
                DoubleAnimation da = new DoubleAnimation
                {
                    From = 1,
                    To = 1.1,
                    AutoReverse = true,
                    Duration = TimeSpan.FromMilliseconds(1000)
                };

                // Create ScaleTransform with center in the middle of the image
                ScaleTransform st = new ScaleTransform
                {
                    CenterX = CycleImage.ActualWidth/2,
                    CenterY = CycleImage.ActualHeight/2
                };

                // Apply ScaleTransform to the image
                CycleImage.RenderTransform = st;

                // Animate X and Y scale the same for proportional scaling
                st.BeginAnimation(ScaleTransform.ScaleXProperty, da);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, da);
            }
        }
    }
}
