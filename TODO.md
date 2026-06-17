# TODO: Keyboard-Only Editor Support

## Task: Make editor usable without mouse

### Phase 1: Find/Replace Overlay Keyboard Support ✓ DONE
- [x] Add Enter key to trigger "Find Next" in Find/Replace overlay
- [x] Add Escape key to close Find/Replace overlay
- [x] Ensure Tab key navigates between search/replace fields (via TabIndex)
- [x] Auto-focus Search field when overlay opens (Ctrl+F)

### Phase 2: Tab Keyboard Support  
- [x] Add keyboard shortcut to close current tab (already exists - Ctrl+W, Ctrl+F4)
- [x] Make Ctrl+Tab/Ctrl+Shift+Tab work reliably for tab switching (verified)
- [ ] Add Ctrl+K, Ctrl+W as alternate close shortcut (optional)

### Phase 3: Focus Management
- [ ] Ensure Tab key navigates between editor and other controls
- [ ] Verify Alt key activates menu bar
- [ ] Check menu keyboard navigation (underlined mnemonics)

### Phase 4: Testing
- [ ] Verify all keyboard shortcuts work
- [ ] Test tab switching without mouse
- [ ] Test find/replace without mouse
