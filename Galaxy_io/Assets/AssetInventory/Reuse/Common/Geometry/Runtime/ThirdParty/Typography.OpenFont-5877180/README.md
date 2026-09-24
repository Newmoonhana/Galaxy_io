# Typography.OpenFont

This folder contains the `netstandard2.0` build of LayoutFarm Typography.OpenFont from commit
`5877180c7c5271091379a0eaf9f03ab6ebd256b3`.

Project: https://github.com/LayoutFarm/Typography

Text Studio 3D uses the managed CFF1 reader and Type 2 charstring evaluator. The dependency is
not used to claim CFF2 or non-default variable-instance support: the selected revision contains
an empty CFF2 parser stub and does not apply variation deltas to emitted outlines. Those formats
remain explicitly diagnosed by Text Studio's provider factory.

The upstream project identifies the overall project license as MIT and notes permissively
licensed contributions, including Apache-2.0 CFF reader work. Both license texts are retained in
this folder.
