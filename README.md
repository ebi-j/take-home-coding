# Release Retention

This solution contains a service that calculates the release to be retained by the input projects, releases, deployments and environments.

It takes `numberOfReleasesToKeep` as the parameter, and returns a list of releases to keep per project and environment. A release in this context refers to a release from a project that has been deployed to an environment.

The resolution will not contain the release which has never been deployed, or belongs to a phantom project (does not exist in the provided project list), or deployed to a phantom environment (does not exist in the provided environment).

## What's in the solution?

### ReleaseRetention.Core

This project contains the contracts for reading the data from a data source, in this solution, from json files.

It also contains a service, which holds the core retention logic.

### ReleaseRetention.Tests

This project contains the test data and the unit tests.

## Improvements

### Think about a real world business case

The implementation may change based on the real business case.

For example, it is reasonable to assume a feature to automate the release retention based on some certain policies. In this case, we may have a task scheduler runs once a day, which reads the policies from the database, as well as the projects, releases, deployments, and environments. Release and deployment data could be very big, if the projects have been running for a few years. The current approach reads the full list to the memory and do the filtering in the memory, it will have performance issues when the input data is huge. The more common approach would be querying the database by applying the policies as the filtering criteria to get a subset of data.

### What if I want to keep most recent undeployed releases as well?

It's quite common that people want to keep the most recent releases even they haven't been deployed.

For example, production deployment will normally paused for approval. In this case, we don't want to delete the most recent production release. This solution is not catering for undeployed releases, as it is not part of the requirements. However, we could extend to handle it by `left join` deployment, and consider `Created` in the aggregation of the `datetime`.

### Input sanitization

The input may contain corrupt data. Ideally, we could throw early. E.g. invalid project id and invalid environment id. The current approach will automatically exclude them due to `join`, but user may confuse when they input "Project-3" but gets no response. However, it will be less an issue if the inputs are all from the database.

### Better logging message

This is a minor UX improvement, we could log "most recent / 2nd recent / ..." rather than "Top {Index}" for better reading experience.

### Abstraction

We could abstract the service for better DI, so as the logger (currently it's a static method). I didn't do it in this solution as I don't want to over engineer the solution.
