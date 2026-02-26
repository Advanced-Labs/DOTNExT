# TesterInternal Project

This project is for **white-box** testing of the Scynapse runtime, 
including testing of internal Scynapse runtime APIs.

This project has 'friend' access to the internal API surface of the Scynapse runtime.

The following projects use `[InternalsVisibleTo]` assembly attributes to grant internal access privilege to this test project:

- `Scynapse.dll`
- `ScynapseRuntime.dll`
- `ScynapseAzureUtils.dll`

This project may contains a mixture of unit and system tests, including starting test silos.
