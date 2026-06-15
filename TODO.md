# Dirty-Check on Close Implementation

## Task
Implement dirty-check prompt when closing tabs (CloseTab and CloseActiveTab) to prompt user with Save/Don't Save/Cancel options when closing dirty documents.

## Steps

1. [x] Add ConfirmDirtyClose message to Messages.cs
2. [x] Modify EditorViewModel.CloseActiveTab to check dirty and prompt
3. [x] Modify EditorViewModel.CloseTab to check dirty and prompt
4. [x] Test the implementation (build succeeded)

## Implementation Details

- When closing a dirty document, prompt user with three options:
  - Save: Save the document first, then close
  - Don't Save: Close without saving (discard changes)
  - Cancel: Do not close the tab

- Files modified:
  - src/Core/Messages.cs - Added ConfirmDirtyClose message and DirtyCloseResponse constants
  - src/ViewModels/EditorViewModel.cs - Implemented dirty-check logic in CloseTab/CloseActiveTab
