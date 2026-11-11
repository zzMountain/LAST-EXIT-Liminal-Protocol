mergeInto(LibraryManager.library, {
  LastExitReport: function (json) {
    var state = JSON.parse(UTF8ToString(json));
    var bounds = Module.canvas.getBoundingClientRect();
    var scaleX = bounds.width / Module.canvas.width;
    var scaleY = bounds.height / Module.canvas.height;
    function convert(value) {
      if (!value || typeof value !== 'object') return;
      if (typeof value.x === 'number' && typeof value.y === 'number') {
        value.x = bounds.left + value.x * scaleX;
        value.y = bounds.top + value.y * scaleY;
      }
      Object.keys(value).forEach(function (key) { convert(value[key]); });
    }
    convert(state);
    state.renderDensity = Module.canvas.width / bounds.width;
    state.cssWidth = bounds.width;
    state.cssHeight = bounds.height;
    window.lastExitState = state;
  }
});
