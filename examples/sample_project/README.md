# sample_project

This folder is a placeholder for the project you want to drive in the example.

The example scripts refer to a project named `sample_project` at a path such as
`C:\projects\sample_project.elk`. EPLAN project files (`.elk`) are created by
EPLAN itself, so this repository does not ship one.

To run the example against your own data:

1. Create or pick any EPLAN P8 project.
2. Pass its path to the example, e.g.:

   ```
   python ../quickstart.py --project "C:\path\to\sample_project.elk"
   ```

Any project works — the example only opens it, lists its pages, and exports a
PDF. It does not modify the project.
