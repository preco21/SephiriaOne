# Third-party notices

SephiriaOne `0.5.0` and later embed the unmodified `net472/0Harmony.dll` from
[Lib.Harmony 2.4.2](https://www.nuget.org/packages/Lib.Harmony/2.4.2), the
dependency-merged build supplied by the [Harmony project](https://github.com/pardeike/Harmony).
The package's license is also embedded as
`SephiriaOne.Dependencies.Harmony.LICENSE`. The following is that license:

```text
MIT License

Copyright (c) 2017 Andreas Pardeike

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

Game assemblies are compile-time references from the user's installation and
are not included in the addon. No code or assets from SephiriaChoiceExpander
are included.

Since `0.21.0`, JSON localization uses the game's installed `Newtonsoft.Json.dll`.
The addon does not redistribute that assembly or any native font assets.
