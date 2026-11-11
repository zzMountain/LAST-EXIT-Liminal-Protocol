mergeInto(LibraryManager.library, {
    LastExitPlatform_Initialize: function(target, levelCount) {
        window.LastExitPlatform.bind(UTF8ToString(target), levelCount,
            function(object, method, payload) { SendMessage(object, method, payload); });
    },
    LastExitPlatform_Ready: function() { window.LastExitPlatform.markReady(); },
    LastExitPlatform_Gameplay: function(active) { window.LastExitPlatform.setGameplay(active !== 0); },
    LastExitPlatform_Save: function(json) { window.LastExitPlatform.save(UTF8ToString(json)); },
    LastExitPlatform_Ad: function() { window.LastExitPlatform.showAd(); }
});
