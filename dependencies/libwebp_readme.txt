To update webp libraries:
Open Developer Command Prompt:

VC\Auxiliary\Build\vcvarsall.bat x64 -vcvars_ver=14.2
cd C:\git\ImageViewer\dependencies\libwebp
nmake /f Makefile.vc CFG=release-static RTLIBCFG=static OBJDIR=output

copy files from the  dependencies\libwebp\output\release-static\x64\lib dir to the dependencies\lib dir