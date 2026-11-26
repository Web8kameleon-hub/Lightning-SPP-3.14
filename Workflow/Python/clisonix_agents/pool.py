"""Bounded in-process async pool adapted from Clisonix agents/pool.py concepts.

One active call per worker; scale up on admission, retire excess idle workers.
Handlers must cooperate with asyncio cancellation and own their resource cleanup.
This pool has no persistence, automatic retry, device access or implied AI model.
"""

import asyncio
import math
from collections.abc import Awaitable, Callable
from typing import Any


class AgentPool:
    def __init__(self, handler: Callable[[Any], Awaitable[Any]], *, min_instances=1,
                 max_instances=8, max_queue_size=128, task_timeout=300.0,
                 idle_timeout=30.0):
        if (any(type(v) is not int for v in (min_instances, max_instances, max_queue_size))
                or not 1 <= min_instances <= max_instances <= 32
                or not 1 <= max_queue_size <= 10000):
            raise ValueError("Require 1 <= min <= max <= 32 and queue capacity 1–10000")
        for value in (task_timeout, idle_timeout):
            if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value) or value <= 0:
                raise ValueError("Timeouts must be finite and positive")
        if not callable(handler):
            raise TypeError("An async task handler is required")
        self._handler = handler
        self._min = min_instances
        self._max = max_instances
        self._timeout = task_timeout
        self._idle_timeout = idle_timeout
        self._queue = asyncio.Queue(maxsize=max_queue_size)
        self._workers = set()
        self._pending = set()
        self._running = False
        self._busy = 0
        self.completed = 0
        self.failed = 0

    async def __aenter__(self):
        if self._running or self._workers:
            raise RuntimeError("Pool is already running")
        self._running = True
        for _ in range(self._min):
            self._spawn()
        return self

    async def __aexit__(self, *_):
        await self.close()

    def _spawn(self):
        worker = asyncio.create_task(self._worker())
        self._workers.add(worker)
        worker.add_done_callback(self._workers.discard)

    @property
    def stats(self):
        return {"instances": len(self._workers), "busy": self._busy,
                "queued": self._queue.qsize(), "pending": len(self._pending),
                "completed": self.completed, "failed": self.failed}

    async def submit(self, payload):
        if not self._running:
            raise RuntimeError("Start the pool with async with before submitting")
        future = asyncio.get_running_loop().create_future()
        # Reject saturation immediately: never create an unbounded set of waiters.
        self._queue.put_nowait((payload, future))
        self._pending.add(future)
        demand = self._busy + self._queue.qsize()
        while len(self._workers) < min(self._max, demand):
            self._spawn()
        try:
            return await future
        finally:
            self._pending.discard(future)
            if not future.done():
                future.cancel()

    async def _worker(self):
        while self._running:
            try:
                payload, future = await asyncio.wait_for(self._queue.get(), self._idle_timeout)
            except TimeoutError:
                if len(self._workers) > self._min:
                    # Remove synchronously so simultaneous idle retirements preserve minimum.
                    self._workers.discard(asyncio.current_task())
                    return
                continue
            task = None
            try:
                if future.cancelled():
                    continue
                self._busy += 1
                task = asyncio.create_task(self._execute(payload))
                future.add_done_callback(lambda done, active=task: active.cancel() if done.cancelled() else None)
                try:
                    result = await asyncio.wait_for(task, self._timeout)
                except asyncio.CancelledError:
                    future.cancel()
                    if not self._running:
                        raise
                except Exception as error:
                    self.failed += 1
                    if not future.done():
                        future.set_exception(error)
                else:
                    self.completed += 1
                    if not future.done():
                        future.set_result(result)
            finally:
                if task is not None:
                    self._busy -= 1
                self._queue.task_done()

    async def _execute(self, payload):
        return await self._handler(payload)

    async def close(self):
        self._running = False
        for future in tuple(self._pending):
            future.cancel()
        workers = tuple(self._workers)
        for worker in workers:
            worker.cancel()
        await asyncio.gather(*workers, return_exceptions=True)
        self._workers.clear()
        while not self._queue.empty():
            _, future = self._queue.get_nowait()
            future.cancel()
            self._queue.task_done()
