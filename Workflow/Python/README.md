# Clisonix Python agent integration

`clisonix_agents/registry.py` is a byte-preserving copy of `agents.py` from
`clisonix/clisonix.com` revision
`a5587565675e7e934a29e356d6e2b3f8bd312de5`, Git blob
`b95ad3ac29fa19bd9358e77de97ba7420e85deb3`.
It provides normalized agent registration and health dispatch. Its core agent
methods return action descriptors; they do not collect documents, run a model,
or prove deployed service health.

`pool.py` is an SPP adaptation of the bounded queue, instance limit and idle
scaling concepts inspected in upstream `agents/pool.py` at that revision.
It accepts a real asynchronous handler and Python 3.11 or later. It starts at
`min_instances`, grows on admitted demand up to `max_instances` (maximum 32),
and retires excess idle workers. Queue overflow raises `asyncio.QueueFull`.
Handler errors and execution timeouts propagate to the caller. Caller
cancellation cancels the active handler; closing cancels queued/active work
and awaits worker termination. Handlers must cooperate with cancellation.
Metrics count actual handler completions/failures, not AI accuracy.

Example from this directory (hashes actual files, no AI inference):

```python
import asyncio
import hashlib
from pathlib import Path
from clisonix_agents import AgentPool

async def hash_file(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

async def main(paths):
    async with AgentPool(hash_file, max_instances=4, max_queue_size=128) as pool:
        return await asyncio.gather(*(pool.submit(path) for path in paths))
```

The pool schedules asynchronous work in one process; it does not provide CPU
parallelism or distributed execution. Handler execution has a deadline; queue
time is separate. Submitting more than queue capacity without consuming results
can reject work. Submit bounded batches and handle overflow explicitly.
Pool state is in memory, with no automatic retry. For customer documents,
retain the existing SPP owner-scoped, permission-checked and durable workflow
boundary. This library is not registered as a browser tool or enabled as a
production service. No real model classification or throughput benchmark is
claimed. The upstream document agent's assumed accuracy of 0.95 and its direct
localhost Ocean endpoint were not imported.

Verification from the repository root:

```powershell
python -m unittest discover -s Workflow/Python -p test_agents.py -v
python -m compileall -q Workflow/Python
```
