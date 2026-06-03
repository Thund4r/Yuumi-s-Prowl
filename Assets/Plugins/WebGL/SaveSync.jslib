mergeInto(LibraryManager.library, {
  // Flushes Unity's in-memory WebGL filesystem (where persistentDataPath lives) out to the
  // browser's IndexedDB, so files written this session survive a page refresh or close.
  // FS.syncfs(false, ...) means "persist memory -> IndexedDB".
  SyncSaveFiles: function () {
    try {
      if (typeof FS !== 'undefined' && FS.syncfs) {
        FS.syncfs(false, function (err) {
          if (err) console.error('SaveSync: FS.syncfs failed', err);
        });
      }
    } catch (e) {
      console.error('SaveSync: exception', e);
    }
  }
});
