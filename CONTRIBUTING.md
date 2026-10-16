# Contributing to Lastlight

Thanks for taking an interest in Lastlight. Bug reports and reproducible feedback are welcome even if you do not write C#.

Before filing an issue, check existing reports and include the release/build version, OS and graphics driver, reproduction steps, expected versus actual results, and relevant errors or screenshots. Please avoid posting private saves without inspecting their contents.

For proposed code changes, first open an issue describing the goal. Keep the deterministic core isolated from graphics, preserve serialized world formats, include an executable regression in `src/Lastlight.Checks` when practical, and avoid checked-in generated assets or test outputs. Run `dotnet build Lastlight.sln -c Release` and relevant checks.

**Licensing:** The original project is [MIT licensed](LICENSE). By submitting a contribution for inclusion, you confirm you have the right to contribute it under the MIT License. Clearly identify any third-party materials, including assets, and include their attribution and applicable license terms. Do not submit assets or code with incompatible redistribution restrictions.
