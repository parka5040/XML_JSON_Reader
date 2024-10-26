# JSON/XML Visual Parser
A visual parser and editor written in C# that's been rewritten from the ground up from my original Python design. It uses Avalonia and the .NET 8.0 runtime framework (shouldn't be required if I did it right).

This is completely open source and exists for the purposes of making data engineering and planning easier. While web apps do exist, this does provide for a more secure, local way to parse through semistructured data. Also, allows for a lot more rapid in-place editing of the original file with proper saving and searching tools that the web apps didn't do satisfactorily for my purposes.

## Table of Contents
- [Features](#features)
- [Installation](#installation)
- [Building from Source](#building-from-source)
- [Usage Guide](#usage-guide)
  - [Opening Files](#opening-files)
  - [Editing Files](#editing-files)
  - [Visual Tree Navigation](#visual-tree-navigation)
- [Technical Details](#technical-details)
- [Development](#development)
- [Troubleshooting](#troubleshooting)

## Features
- **Visual Tree View**: Hierarchical visualization of JSON and XML structures
- **Dual-pane Interface**: Synchronized text and tree views
- **Advanced Search**:
  - Real-time highlighting
  - Support for regex and regular searching
  - Scope-based searching (names, values, attributes)
  - Case-sensitive options
- **In-place Editing**:
  - Direct editing
  - Attribute management
  - Node deletion
- **Format Support**:
  - JSON parsing and formatting
  - XML parsing and formatting
- **Cross-platform Support**:
  - Windows
  - Linux*
  - macOS*
  *Note: You do need to build the solutions yourself. A guide is provided below. 

## Installation

### Pre-built Binaries
FOR WINDOWS ONLY:
Download the SemistructuredParser.exe file that's provided from the Releases page. 

### System Requirements
- Windows 10/11
- 100MB disk space
- 4GB RAM recommended (8GB was the lowest machine I tested this on)

## Building from Source

### Prerequisites
- .NET 8 SDK

### Creating a Standalone Executable
Windows (PowerShell):
```powershell
.\publish.ps1 -Runtime win-x64
```

Linux/macOS:
```bash
./publish.sh Release linux-x64
```

Clone my repo and run the respective files for your OS. 

## Usage Guide

### Opening Files
The Open Button should allow for the selection of any JSON or XML data. If the file cannot be opened, this is an indication that the JSON or XML file is malformed or otherwise not properly formatted. For future versions, I will see about doing diagnostics on this. 

### Editing Files

#### Text Editor Mode
- Direct text editing with syntax highlighting
- Real-time validation
- Auto-formatting on save

#### Visual Tree Editing
1. Click the "Edit Mode" button in the toolbar. It should toggle and indicate "On"
2. Use the edit controls that appear next to each node:
   - ✎ (Edit): Edit node properties
   - 🗑️ (Delete): Delete node
3. When editing a node:
   - Edit the name and value
   - Add/remove attributes
## Technical Details
### Key Components
- **Parser.Core**: Contains the document model and parsing interfaces
- **Parser.Services**: Implements JSON and XML parsing services
- **Parser.UI**: AvaloniaUI-based user interface

### Technologies Used
- .NET 8
- AvaloniaUI 11.0.6
- System.Text.Json
- System.Xml.Linq