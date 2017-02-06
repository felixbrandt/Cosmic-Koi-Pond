using System;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Windows.Input;
using System.Windows.Forms;
using Microsoft.Kinect;
using System.Timers;

namespace CosmicKoiPond
{
    /// <summary>
    /// Interaktionslogik für VideoOutput.xaml
    /// </summary>
    public partial class VideoOutput
    {
        /// <summary>
        /// Global scale for all elements on the canvas
        /// </summary>
        private const float Scale = 0.25f;

        /// <summary>
        /// List of all spawning creatures currently visible on the  screen
        /// </summary>
        private readonly List<SpawningCreature> _spawningCreatures = new List<SpawningCreature>();

        /// <summary>
        /// Mapping each body to its own lillypad
        /// </summary>
        private readonly Dictionary<Body, Lilypad> _lillypads = new Dictionary<Body, Lilypad>();

        /* Swarm URIs */
        private static Uri _spawnUriSwarm; // Spawn
        private static Uri _cycleUriSwarm; // Cycle

        /* Skater URIs */
        private static Uri _spawnUriSkater; // Spawn
        private static Uri _cycleUriSkater; // Cycle

        /* Lilypad URIs */
        private static Uri _spawnUriLillypad; // Spawn
        private static Uri _cycleUriLillypad; // Cycle
        private static Uri _despawnUriLillypad; // Despawn

        /* Different URIs for fishes for randomness */
        private readonly List<Uri> _spawnUrisFish = new List<Uri>();
        private readonly List<Uri> _transUrisFish = new List<Uri>();
        private readonly List<Uri> _cycleUrisFish = new List<Uri>();

        /* Different URIs for sounds of different creatures */
        private static readonly List<Uri> SoundsFish = new List<Uri>();
        private static readonly List<Uri> SoundsSwarm = new List<Uri>();
        private static readonly List<Uri> SoundsSkater = new List<Uri>();

        /// <summary>
        /// Maximum count of SpawningCreatures on the screen at a single time
        /// </summary>
        private const int MaxCreatureCount = 25;

        // Time the last creature was created (for preventing spawningCreature spawn apocalypse)
        private DateTime _lastFishCreatedAt = DateTime.Now;
        private DateTime _lastSwarmCreatedAt = DateTime.Now;
        private DateTime _lastSkaterCreatedAt = DateTime.Now;

        internal MovieState State { get; set; }

        /// <summary>
        /// Initialize the window
        /// </summary>
        public VideoOutput()
        {
            InitializeComponent();

            // Initialize image URIs
            // (Have to do this this way because XamlAnimatedGif does not support relative URIs)
            // (https://github.com/XamlAnimatedGif/WpfAnimatedGif/issues/25)
            String folder = SelectFolder();

            if (folder == null)
            {
                // Folder selection dialog was closed
                System.Windows.Application.Current.Shutdown();
            }

            // Add trailing slash to path
            folder += "/Animations/";

            // Load all images
            Uri spawnUriFish = new Uri(folder + ("fish_scale_1.gif"));
            Uri transUriFish = new Uri(folder + ("fish_scale_2.gif"));
            Uri cycleUriFish = new Uri(folder + ("fish_scale_3.gif"));
            Uri spawnUriTigerFish = new Uri(folder + ("fish_tiger_1.gif"));
            Uri transUriTigerFish = new Uri(folder + ("fish_tiger_2.gif"));
            Uri cycleUriTigerFish = new Uri(folder + ("fish_tiger_3.gif"));
            Uri spawnUriSpotFish = new Uri(folder + ("fish_spot_1.gif"));
            Uri transUriSpotFish = new Uri(folder + ("fish_spot_2.gif"));
            Uri cycleUriSpotFish = new Uri(folder + ("fish_spot_3.gif"));
            _spawnUriSwarm = new Uri(folder + ("swarm_1.gif"));
            _cycleUriSwarm = new Uri(folder + ("swarm_2.gif"));
            _spawnUriSkater = new Uri(folder + ("pondskater_spawn.gif"));
            _cycleUriSkater = new Uri(folder + ("pondskater_cycle.gif"));
            _spawnUriLillypad = new Uri(folder + ("lily_spawn.gif"));
            _cycleUriLillypad = new Uri(folder + ("lily_cycle.gif"));
            _despawnUriLillypad = new Uri(folder + ("lily_despawn.gif"));

            // Add images to arrays
            _spawnUrisFish.Add(spawnUriFish);
            _spawnUrisFish.Add(spawnUriTigerFish);
            _spawnUrisFish.Add(spawnUriSpotFish);

            _transUrisFish.Add(transUriFish);
            _transUrisFish.Add(transUriTigerFish);
            _transUrisFish.Add(transUriSpotFish);

            _cycleUrisFish.Add(cycleUriFish);
            _cycleUrisFish.Add(cycleUriTigerFish);
            _cycleUrisFish.Add(cycleUriSpotFish);

            // Add different fish sounds
            SoundsFish.Add(new Uri("Sound/fish01.wav", UriKind.Relative));
            SoundsFish.Add(new Uri("Sound/fish02.wav", UriKind.Relative));
            SoundsFish.Add(new Uri("Sound/fish03.wav", UriKind.Relative));
            SoundsFish.Add(new Uri("Sound/fish04.wav", UriKind.Relative));
            SoundsFish.Add(new Uri("Sound/fish05.wav", UriKind.Relative));
            SoundsFish.Add(new Uri("Sound/fish06.wav", UriKind.Relative));
            SoundsFish.Add(new Uri("Sound/fish07.wav", UriKind.Relative));
            SoundsFish.Add(new Uri("Sound/fish08.wav", UriKind.Relative));

            // Add different swarm sounds
            SoundsSwarm.Add(new Uri("Sound/swarm01.wav", UriKind.Relative));
            SoundsSwarm.Add(new Uri("Sound/swarm02.wav", UriKind.Relative));
            SoundsSwarm.Add(new Uri("Sound/swarm03.wav", UriKind.Relative));
            SoundsSwarm.Add(new Uri("Sound/swarm04.wav", UriKind.Relative));

            // Add Skater Sounds
            SoundsSkater.Add(new Uri("Sound/fish01.wav", UriKind.Relative));
            SoundsSkater.Add(new Uri("Sound/fish02.wav", UriKind.Relative));
            SoundsSkater.Add(new Uri("Sound/fish03.wav", UriKind.Relative));
            SoundsSkater.Add(new Uri("Sound/fish04.wav", UriKind.Relative));
            SoundsSkater.Add(new Uri("Sound/fish05.wav", UriKind.Relative));
            SoundsSkater.Add(new Uri("Sound/fish06.wav", UriKind.Relative));
            SoundsSkater.Add(new Uri("Sound/fish07.wav", UriKind.Relative));
            SoundsSkater.Add(new Uri("Sound/fish08.wav", UriKind.Relative));

            // Create main loop timer, fires every 6 seconds
            System.Timers.Timer mainTimer = new System.Timers.Timer();
            mainTimer.Elapsed += MainLoop;
            mainTimer.Interval = 6000; // in ms
            mainTimer.AutoReset = true;
            mainTimer.Enabled = true;

            CreditsVideo.Pause();
            BackgroundVideo.Pause();
            // StartVideo.Play();

            ChangeState(MovieState.Intro);
            //State = MovieState.Intro;
        }

        /// <summary>
        /// Switch to a MovieState
        /// </summary>
        /// <param name="state">MovieState to switch to (Playing or Credits)</param>
        private void ChangeState(MovieState state)
        {
            switch(state)
            {
                case MovieState.Playing:
                    // Show and play BackgroundVideos
                    BackgroundVideo.Play();

                    // Hide, Pause and Rewind CreditsVideo
                    StartVideo.Opacity = 0;
                    StartVideo.Position = new TimeSpan(0);
                    StartVideo.Pause();

                    // Pause and rewind Credits sound
                    OutroMusic.Position = new TimeSpan(0);
                    OutroMusic.Pause();
                    
                    State = MovieState.Playing;
                    break;
                case MovieState.Credits:
                    // Start OutroMusic
                    OutroMusic.Play();

                    // Show and play CreditsVideo
                    CreditsVideo.Opacity = 1;
                    CreditsVideo.Play();

                    // Pause and Rewind BackgroundVideo
                    BackgroundVideo.Position = new TimeSpan(0);
                    BackgroundVideo.Pause();                    

                    // Pause and Rewind BackgroundSound
                    // BackgroundMusic.Volume = 0;
                    BackgroundMusic.Position = new TimeSpan(0);
                    BackgroundMusic.Pause();

                    State = MovieState.Credits;
                    break;
                case MovieState.Intro:
                    // Start BackgroundMusic
                    BackgroundMusic.Play();

                    // Show and Play BackgroundVideo
                    StartVideo.Play();
                    StartVideo.Opacity = 1;

                    // Hide, Pause and Rewind Credits
                    CreditsVideo.Opacity = 0;
                    CreditsVideo.Position = new TimeSpan(0);
                    CreditsVideo.Pause();

                    // Pause and rewind credits sound
                    OutroMusic.Position = new TimeSpan(0);
                    OutroMusic.Pause();

                    State = MovieState.Intro;
                    break;
            }
        }


        /// <summary>
        /// Loop for reoccuring events, gets executed every 6 seconds
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">Event</param>
        private void MainLoop(object sender, ElapsedEventArgs e)
        {
            // Check skater spawn -> 5 Skaters per Minute
            // Timer gets fired 10 times a minute, Skater chance should be 50%
            int chance = 50; // chance in %
            Random random = new Random();

            if (random.Next(100) < chance)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(delegate
                {
                    CreateAnimatedCreature(CreatureType.Skater, 0);
                });
            }
        }

        /// <summary>
        /// Opens a 'Open folder' dialog
        /// </summary>
        /// <returns></returns>
        private String SelectFolder()
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog
            {
                Description = @"Select 'Cosmic-Koi-Pond' folder",
                ShowNewFolderButton = false
            };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Console.WriteLine(dialog.SelectedPath);
                return dialog.SelectedPath;
            } else
            {
                return null;
            }
            
        }

        /// <summary>
        /// Keys for testing various functions when no Kinect is available
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.F:
                    CreateAnimatedCreature(CreatureType.Fish, 0, Direction.Left);
                    break;
                case Key.G:
                    CreateAnimatedCreature(CreatureType.Fish, 0, Direction.Right);
                    break;
                case Key.S:
                    CreateAnimatedCreature(CreatureType.Swarm, 0);
                    break;
                case Key.D:
                    CreateAnimatedCreature(CreatureType.Skater, 0);
                    break;
            }
        }


        /// <summary>
        /// Spawn an animated creature on the canvas
        /// </summary>
        /// <param name="type">Type of SpawningCreature</param>
        /// <param name="creatorTrackingId">Tracking ID of the body who did the gesture</param>
        /// <param name="direction">Direction in which to move (fish only)</param>
        /// <returns>Created SpawningCreature</returns>
        public SpawningCreature CreateAnimatedCreature(CreatureType type, ulong creatorTrackingId, Direction direction = Direction.Null)
        {
            // For all fishes: Check if they are still in frame.
            // If they are not, remove them.
            CheckFishRemoval();

            // Don't let things spawn while credits are playing
            if (State == MovieState.Credits)
            { 
                Console.WriteLine(@"I'm not spawning creatures while in the credits.");
                return null;
            }

            if (creatorTrackingId != 0)
            {
                // Do an animation for the lillypad that belongs to the player who did the gesture as a visual feedback
                _lillypads[GetBodyByTrackingId(creatorTrackingId)].TriggerGesture();
            }

            // Test if the creature can be created (max amount of creatures on canvas)
            if (_spawningCreatures.Count >= MaxCreatureCount)
                return null;

            // Prepare variables for spawning
            float scale = Scale;
            Uri spawnUri = null;
            Uri transUri = null;
            Uri cycleUri = null;
            Uri soundUri = null;
            Random random = new Random();

            switch (type)
            {
                case CreatureType.Fish:
                    if (_lastFishCreatedAt > DateTime.Now.Subtract(new TimeSpan(0, 0, 2)))
                    {
                        return null; // Don't create a fish because the last one was created in the last 2 seconds
                    }
                    
                    // New fish will be created
                    _lastFishCreatedAt = DateTime.Now;

                    // Select a random fish from the list of available ones
                    int i = random.Next(_spawnUrisFish.Count);
                    spawnUri = _spawnUrisFish[i];
                    transUri = _transUrisFish[i];
                    cycleUri = _cycleUrisFish[i];

                    // Select a random fish sound from the list of available ones
                    soundUri = SoundsFish[random.Next(SoundsFish.Count)];

                    break;
                case CreatureType.Swarm:
                    if (_lastSwarmCreatedAt > DateTime.Now.Subtract(new TimeSpan(0, 0, 7)))
                    {
                        return null; // Don't create a swarm because the last one was created in the last 7 seconds
                    }

                    // New swarm will be created
                    _lastSwarmCreatedAt = DateTime.Now;

                    // Set swarm scale
                    scale *= 1.8f;

                    // Set swarm image URI
                    spawnUri = _spawnUriSwarm;
                    cycleUri = _cycleUriSwarm;

                    // Select a random swarm sound from the list of available ones
                    soundUri = SoundsSwarm[random.Next(SoundsSwarm.Count)];

                    break;
                case CreatureType.Skater:
                    if (_lastSkaterCreatedAt > DateTime.Now.Subtract(new TimeSpan(0, 0, 5)))
                    {
                        return null; // Don't create a skater because the last one was created in the last 5 seconds
                    }

                    // New skater will be created
                    _lastSkaterCreatedAt = DateTime.Now;

                    // Set skater scale
                    scale *= 1.2f;

                    // Set skater image URI
                    spawnUri = _spawnUriSkater;
                    cycleUri = _cycleUriSkater;

                    // Select a random sound from the list of available ones
                    soundUri = SoundsSkater[random.Next(SoundsSkater.Count)];

                    break;
            }

            // Creature is created with the right params
            SpawningCreature spawningCreature = new SpawningCreature(scale, type, spawnUri, cycleUri, soundUri, direction, this, transUri);

            // Add creature to list of all creatures
            _spawningCreatures.Add(spawningCreature);

            return spawningCreature;
        }

        /// <summary>
        /// Gets called when a new player enters the kinect view area.
        /// Creates a lilypad on the canvas which responds to the users gestures
        /// </summary>
        /// <param name="body">Body entering the kinect field of view</param>
        public void OnPlayerEnter(Body body)
        {
            // Create a new lilypad and assign it to the body in lillypads dictionary
            _lillypads.Add(body, new Lilypad(Scale * 1.6f, _spawnUriLillypad, _cycleUriLillypad, _despawnUriLillypad, this));
        }

        /// <summary>
        /// Gets called when a player leaves the kinect view area.
        /// Despawns the lilypad and removes it from the lilypad dictionary
        /// </summary>
        /// <param name="body">Body leaving the kinect field of view</param>
        public void OnPlayerLeave(Body body)
        {
            _lillypads[body].Despawn();
            _lillypads.Remove(body);
        }

        /// <summary>
        /// Checks if fishes are outside the windows visible area and removes them if they are
        /// </summary>
        private void CheckFishRemoval()
        {
                Console.WriteLine(@"CheckFishRemoval() ({0} spawningCreature(es))", _spawningCreatures.Count);
            
                for (int i = 0; i < _spawningCreatures.Count; i++)
                {
                    if (IsInvisible(_spawningCreatures[i]))
                    {
                        MyCanvas.Children.Remove(_spawningCreatures[i].CycleImage);

                        _spawningCreatures.Remove(_spawningCreatures[i]);

                        Console.WriteLine(@"CheckFishRemoval() ({0} spawningCreature(es))", i--);
                    }

                }
                
        }

        /// <summary>
        /// Get a body by its tracking id. Body has to be in the frame (of course...)
        /// </summary>
        /// <param name="trackingId">Tracking ID</param>
        /// <returns>Body with that tracking ID</returns>
        private Body GetBodyByTrackingId(ulong trackingId)
        {
            // Iterate through bodies in the frame
            foreach (Body body in _lillypads.Keys)
            {
                if (body.TrackingId == trackingId)
                {
                    return body;
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if a SpawningCreature is visible
        /// </summary>
        /// <param name="spawningCreature">SpawningCreature to check</param>
        /// <returns></returns>
        private bool IsInvisible(SpawningCreature spawningCreature)
        {
            Rect bounds = new Rect(0.0-spawningCreature.CycleImage.ActualHeight, 0.0-spawningCreature.CycleImage.ActualHeight, MyCanvas.ActualWidth+spawningCreature.CycleImage.ActualHeight*2, MyCanvas.ActualHeight+spawningCreature.CycleImage.ActualHeight*2);
            Rect fishRect = new Rect(Canvas.GetLeft(spawningCreature.CycleImage), Canvas.GetTop(spawningCreature.CycleImage),
                spawningCreature.CycleImage.ActualWidth, spawningCreature.CycleImage.ActualHeight);

            return !bounds.Contains(fishRect);
        }

        /// <summary>
        /// Background video ended -> initiate Credits playback
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">Event</param>
        private void BackgroundVideo_OnMediaEnded(object sender, RoutedEventArgs e)
        {
            ChangeState(MovieState.Credits);
        }

        private void CreditsVideo_MediaEnded(object sender, RoutedEventArgs e)
        {
            ChangeState(MovieState.Intro);
        }

        private void IntroVideo_OnMediaEnded(object sender, RoutedEventArgs e)
        {
            ChangeState(MovieState.Playing);
        }
    }
}
