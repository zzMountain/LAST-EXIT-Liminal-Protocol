mergeInto(LibraryManager.library, {
  LastExitRenderDensity: function () {
    var bounds = Module.canvas.getBoundingClientRect();
    return bounds.width > 0 ? Module.canvas.width / bounds.width : 1;
  }
});
