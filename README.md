# NuGet.Extention

NuGet Extension is having the following features
- [X] Convert the NuGet reference to Project reference and back to NuGet reference
- [X] Smart upgrade NuGet Version (according to the Assembly version)

# Road map

- [x] Operation Context (per execution, reduce local state), see OperationContext
- [x] Progress bar report
- [x] Refactoring
- [x] Avoid upgrade without changing version
- [x] Pre-build validation
- [ ] Upgrade parent Nugets which is not part of the solution (as long as it don't have files)
- [ ] Support multiple progress bar report
- [ ] Create Nuget package from new project (which don't have package yet)
- [ ] Unchackout project that has not changed after moving back to NuGet (calculate the file hash)
- [ ] Disable NuGet Operations when some of the project not loaded
- [ ] 
- [ ] 