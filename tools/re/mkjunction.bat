@echo off
REM Ghidra's batch launcher can't tolerate the space in the repo path ("Disco Elysium").
REM This creates C:\disco-re as a space-free directory-junction view of the repo so the
REM headless tooling runs cleanly. Directory junctions need no admin rights.
REM Re-run if C:\disco-re is missing (e.g. after the repo moves).
if exist C:\disco-re (
  echo C:\disco-re already exists.
) else (
  mklink /J C:\disco-re "C:\Users\rasha\Documents\Disco Elysium"
)
