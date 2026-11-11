mergeInto(LibraryManager.library, {
  LastExitAudioCue: function (sound) {
    var audio = window.lastExitAudio || (window.lastExitAudio = { events: [], counts: {} });
    var name = UTF8ToString(sound);
    audio.counts[name] = (audio.counts[name] || 0) + 1;
    audio.events.push({ sound: name, time: performance.now() });
    if (audio.events.length > 256) audio.events.shift();
  },
  LastExitAudioState: function (activated, paused, muted, voices) {
    var audio = window.lastExitAudio || (window.lastExitAudio = { events: [], counts: {} });
    audio.activated = !!activated;
    audio.paused = !!paused;
    audio.muted = !!muted;
    audio.voices = voices;
  }
});
