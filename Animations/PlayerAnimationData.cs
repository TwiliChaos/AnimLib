﻿using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AnimLib.Animations {
  /// <summary>
  /// Container for various <see cref="Animation"/>s and data to be attached to an <see cref="AnimPlayer"/>. Manages advancement of frames.
  /// </summary>
  public abstract class PlayerAnimationData {
    /// <summary>
    /// Creates a new instance of <see cref="PlayerAnimationData"/> for the given <see cref="ModPlayer"/>.
    /// </summary>
    /// <param name="modPlayer">The <see cref="ModPlayer"/> instance the animations will belong to.</param>
    /// <exception cref="InvalidOperationException">Animation classes are not allowed to be constructed on a server.</exception>
    /// <exception cref="InvalidOperationException">The mod of the given <see cref="ModPlayer"/> does not contain any classes derived from <see cref="AnimationSource{T}"/>.</exception>
    protected PlayerAnimationData(ModPlayer modPlayer) : this(modPlayer.player, modPlayer.mod) { }

    /// <summary>
    /// Creates a new instance of <see cref="PlayerAnimationData"/> for the given <see cref="Player"/> that belongs to <see cref="Mod"/>.
    /// </summary>
    /// <param name="player">The <see cref="Player"/> instance the animations will belong to.</param>
    /// <param name="mod">The mod that owns the <see cref="IAnimationSource"/>s this will use.</param>
    /// <exception cref="InvalidOperationException">Animation classes are not allowed to be constructed on a server.</exception>
    /// <exception cref="InvalidOperationException">The mod of the given <see cref="ModPlayer"/> does not contain any classes derived from <see cref="AnimationSource{T}"/>.</exception>
    protected PlayerAnimationData(Player player, Mod mod) {
      if (Main.netMode == NetmodeID.Server) {
        throw new InvalidOperationException($"Animation classes are not allowed to be constructed on servers.");
      }
      var sources = AnimLibMod.Instance.AnimationSources;
      if (!sources.ContainsKey(mod)) {
        throw new InvalidOperationException($"{mod.Name} does not contain any classes derived from AnimationSource.");
      }

      this.player = player;
      this.mod = mod;
      var modSources = sources[mod];
      
      animations = new Animation[modSources.Length];
      for (int i = 0; i < modSources.Length; i++) {
        animations[i] = new Animation(this, modSources[i]);
      }
    }

    /// <summary>
    /// Allows you to do things after this <see cref="PlayerAnimationData"/> is constructed.
    /// </summary>
    public virtual void Initialize() { }

    /// <summary>
    /// All <see cref="Animation"/>s that belong to this mod.
    /// </summary>
    public readonly Animation[] animations;

    /// <summary>
    /// The <see cref="Player"/> that is being animated.
    /// </summary>
    public readonly Player player;

    /// <summary>
    /// The <see cref="Mod"/> that owns this <see cref="PlayerAnimationData"/>.
    /// </summary>
    public readonly Mod mod;

    /// <summary>
    /// The <see cref="Animation"/> to retrieve track data from, such as frame duration. This <see cref="Animation"/>'s <see cref="IAnimationSource"/> must contain all tracks that can be used.
    /// <para>By default this is the first <see cref="Animation"/> in <see cref="animations"/>.</para>
    /// </summary>
    public virtual Animation MainAnimation => animations[0];

    /// <summary>
    /// The name of the animation track currently playing.
    /// </summary>
    public string TrackName {
      get => _trackName;
      set {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"{nameof(value)} cannot be empty.");
        if (value != _trackName) {
          _trackName = value;
          Validate(value, true);
        }
      }
    }
    private string _trackName = "Default";

    /// <summary>
    /// Current index of the <see cref="Track"/> being played.
    /// </summary>
    public int FrameIndex { get; private set; }

    /// <summary>
    /// Current time of the <see cref="Frame"/> being played.
    /// </summary>
    public float FrameTime { get; internal set; }

    /// <summary>
    /// Current rotation the sprite is set to.
    /// </summary>
    public float SpriteRotation { get; private set; }

    /// <summary>
    /// Whether or not the animation is currently being played in reverse.
    /// </summary>
    public bool Reversed { get; private set; }

    /// <summary>
    /// Gets the <see cref="Animation"/> from this <see cref="animations"/> where its <see cref="IAnimationSource"/> is of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Type of <see cref="AnimationSource{T}"/></typeparam>
    /// <returns>The <see cref="Animation"/> with the matching <see cref="IAnimationSource"/>.</returns>
    /// <exception cref="ArgumentException"><typeparamref name="T"/> and <see cref="mod"/> are from different assemblies.</exception>
    public Animation GetAnimation<T>() where T : IAnimationSource {
      string tAsmName = typeof(T).Assembly.FullName;
      string modAsmName = mod.Code.FullName;
      if (tAsmName != modAsmName) {
        throw new ArgumentException($"Assembly mismatch: {typeof(T)} is from {tAsmName}; this {nameof(PlayerAnimationData)} is from {modAsmName}");
      }
      foreach (var anim in animations) {
        if (anim.source is T) {
          return anim;
        }
      }
      return null;
    }

    /// <summary>
    /// Check if the <see cref="Animation"/>s will be valid when the given track name.
    /// If <paramref name="updateValue"/> is <see langword="true"/>, all <see cref="Animation.Valid"/> states will be updated.
    /// Returns <see langword="true"/> if the main <see cref="Animation"/> is valid, otherwise <see langword="false"/>.
    /// </summary>
    /// <param name="newTrackName">New value of <see cref="TrackName"/>.</param>
    /// <param name="updateValue">Whether or not to update <see cref="Animation.Valid"/>.</param>
    /// <returns><see langword="true"/> if the main <see cref="Animation"/> is valid, otherwise <see langword="false"/>.</returns>
    private bool Validate(string newTrackName, bool updateValue) {
      if (!updateValue) {
        return MainAnimation.CheckIfValid(newTrackName);
      }

      foreach (var anim in animations) {
        anim.CheckIfValid(newTrackName, updateValue);
      }
      return MainAnimation.Valid;
    }

    /// <summary>
    /// Updates the player animation by one frame, and changes it depending on various conditions.
    /// <para>You must make calls to <see cref="IncrementFrame(string, int, float, int, LoopMode?, Direction?, float)"/> to switch or continue the animation.</para>
    /// </summary>
    /// <example>
    /// Here is an example of updating the animation based on player movement.
    /// <code>
    /// public override void Update() {
    ///   if (Math.Abs(player.velocity.X) &gt; 0.1f) {
    ///     IncrementFrame("Moving");
    ///     return;
    ///   }
    ///   if (player.velocity.Y != 0) {
    ///     IncrementFrame(player.velocity.Y * player.gravDir &lt; 0 ? "Jumping" : "Falling");
    ///     return;
    ///   }
    ///   IncrementFrame("Idle");
    /// }
    /// </code>
    /// </example>
    public abstract void Update();

    /// <summary>
    /// Logic for managing which frame should play.
    /// </summary>
    /// <param name="trackName">Name of the animation track to play/continue.</param>
    /// <param name="overrideFrameIndex">Optional override for the frame to play. This forces a frame to play and prevents normal playback.</param>
    /// <param name="timeOffset">Optional offset to time. To play in reverse, use <paramref name="overrideDirection"/>.</param>
    /// <param name="overrideDuration">Optional override for the duration of the frame.</param>
    /// <param name="overrideDirection">Optional override for the direction the track plays.</param>
    /// <param name="overrideLoopmode">Optional override for how the track loops.</param>
    /// <param name="rotation">Rotation of the sprite, in <strong>radians</strong>.</param>
    /// <exception cref="ArgumentException"><paramref name="trackName"/> was null or whitespace.</exception>
    /// <exception cref="KeyNotFoundException">The value of <paramref name="trackName"/> was not a key in any <see cref="IAnimationSource.tracks"/>.</exception>
    protected void IncrementFrame(string trackName, int overrideFrameIndex = -1, float timeOffset = 0, int overrideDuration = -1, LoopMode? overrideLoopmode = null, Direction? overrideDirection = null, float rotation = 0) {
      if (string.IsNullOrWhiteSpace(trackName)) {
        throw new ArgumentException($"{nameof(trackName)} cannot be empty.", nameof(trackName));
      }
      if (!Validate(trackName, false)) {
        throw new KeyNotFoundException($"\"{trackName}\" is not a valid key for any Animation track.");
      }

      FrameTime += timeOffset;
      SpriteRotation = rotation;

      //Main.NewText($"Frame called: {TrackName}{(Reversed ? " (Reversed)" : "")}, Time: {FrameTime}, AnimIndex: {FrameIndex}/{MainAnimation.CurrentTrack.frames.Length}"); // Debug

      Track track = MainAnimation.source[trackName];
      LoopMode loop = overrideLoopmode ?? track.loop;
      Direction direction = overrideDirection ?? track.direction;
      IFrame[] frames = track.frames;
      int lastFrame = frames.Length - 1;

      if (trackName != TrackName) {
        // Track changed: switch to next track
        TrackName = trackName;
        track = MainAnimation.source[trackName];
        frames = track.frames;
        lastFrame = frames.Length - 1;
        Reversed = direction == Direction.Reverse;
        FrameIndex = Reversed ? lastFrame : 0;
        FrameTime = 0;
      }

      if (overrideFrameIndex >= 0 && overrideFrameIndex <= lastFrame) {
        // If overrideFrame was specified, simply set frame
        FrameIndex = overrideFrameIndex;
        FrameTime = 0;
      }

      // Increment frames based on time (this should rarely be above 1)
      int duration = overrideDuration != -1 ? overrideDuration : frames[FrameIndex].duration;
      if (FrameTime < duration || duration <= 0) {
        return;
      }

      int framesToAdvance = 0;
      while (FrameTime >= duration) {
        FrameTime -= duration;
        framesToAdvance++;
        if (framesToAdvance + FrameIndex > lastFrame) {
          FrameTime %= duration;
        }
      }

      // Loop logic
      switch (direction) {
        case Direction.Forward: {
            Reversed = false;
            if (FrameIndex == lastFrame) {
              // Forward, end of track w/ transfer: transfer to next track
              if (loop == LoopMode.Transfer) {
                TrackName = track.transferTo;
                FrameIndex = 0;
                FrameTime = 0;
              }
              // Forward, end of track, always loop: replay track forward
              else if (loop == LoopMode.Always) {
                FrameIndex = 0;
              }
            }
            // Forward, middle of track: continue playing track forward
            else {
              FrameIndex += framesToAdvance;
            }
            break;
          }
        case Direction.PingPong: {
            // Ping-pong, always loop, reached start of track: play track forward
            if (FrameIndex == 0 && loop == LoopMode.Always) {
              Reversed = false;
              FrameIndex += framesToAdvance;
            }
            // Ping-pong, always loop, reached end of track: play track backwards
            else if (FrameIndex == lastFrame && loop == LoopMode.Always) {
              Reversed = true;
              FrameIndex -= framesToAdvance;
            }
            // Ping-pong, in middle of track: continue playing track either forward or backwards
            else {
              FrameIndex += Reversed ? -framesToAdvance : framesToAdvance;
            }
            break;
          }
        case Direction.Reverse: {
            Reversed = true;
            // Reverse, if loop: replay track backwards
            if (FrameIndex == 0) {
              if (loop == LoopMode.Transfer) {
                TrackName = track.transferTo;
                FrameIndex = 0;
                FrameTime = 0;
              }
              if (loop == LoopMode.Always) {
                FrameIndex = lastFrame;
              }
            }
            // Reverse, middle of track: continue track backwards
            else {
              FrameIndex -= framesToAdvance;
            }
            break;
          }
      }
      FrameIndex = (int)MathHelper.Clamp(FrameIndex, 0, lastFrame);
    }
  }
}