# Compatibility Matrix Template

Date:
Owner:
Release/Commit:

## Environment
- Machine:
- OS version/build:
- OS architecture:
- App package type:
- App process architecture:
- .NET runtime version:
- VC++ runtime status:

## Presentation Software
- Vendor: PowerPoint / WPS
- Edition/channel:
- Version:
- Process name:
- Main class/window signature:
- Architecture (x64/x86):

## Permission Model
- App elevation: Admin / Standard
- Presentation app elevation: Admin / Standard
- Privilege consistency: Match / Mismatch

## Security Context
- Endpoint security/antivirus:
- Hook interception policy (if known):

## Validation Checklist
- Startup compatibility report generated
- No blocking startup issue
- Foreground send command works
- WPS hook/remote hook behavior confirmed
- Degraded path behavior confirmed (if warnings present)
- Override package import/export validated (if used)

## Gate Evidence
- build:
- full test:
- contract/invariant:
- hotspot:

## Result
- Verdict: Pass / Pass with warning / Fail
- Blocking issues:
- Warnings:
- Follow-up actions:
- Rollback plan:
