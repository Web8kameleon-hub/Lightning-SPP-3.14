import asyncio
import hashlib
import tempfile
import unittest
from pathlib import Path

from clisonix_agents import AgentPool, AgentOrchestrator, normalize_agent_name


class AgentTests(unittest.IsolatedAsyncioTestCase):
    def test_copied_registry_normalizes_names(self):
        self.assertEqual("ALBA", normalize_agent_name("ál-bà"))
        self.assertEqual(1, len(AgentOrchestrator().pool_for("ＡＬＢＡ")))

    async def test_scales_and_executes_real_file_hashes(self):
        release = asyncio.Event()
        started = asyncio.Event()
        active = 0
        peak = 0

        async def hash_file(path):
            nonlocal active, peak
            active += 1
            peak = max(peak, active)
            if active == 3:
                started.set()
            try:
                await release.wait()
                return hashlib.sha256(Path(path).read_bytes()).hexdigest()
            finally:
                active -= 1

        with tempfile.TemporaryDirectory() as directory:
            paths = [Path(directory) / str(i) for i in range(6)]
            for i, path in enumerate(paths):
                path.write_bytes(str(i).encode())
            async with AgentPool(hash_file, max_instances=3, idle_timeout=0.02) as pool:
                calls = [asyncio.create_task(pool.submit(path)) for path in paths]
                await asyncio.wait_for(started.wait(), 2)
                self.assertEqual(3, pool.stats["busy"])
                release.set()
                results = await asyncio.gather(*calls)
                self.assertEqual([hashlib.sha256(p.read_bytes()).hexdigest() for p in paths], results)
                self.assertEqual(3, peak)
                async with asyncio.timeout(2):
                    while pool.stats["instances"] != 1:
                        await asyncio.sleep(0)
                self.assertEqual(6, pool.completed)
            self.assertEqual(0, pool.stats["instances"])

    async def test_failures_and_timeout_are_not_success(self):
        async def fail(payload):
            if payload == "error":
                raise ValueError("real failure")
            await asyncio.Event().wait()

        async with AgentPool(fail, task_timeout=0.02) as pool:
            with self.assertRaisesRegex(ValueError, "real failure"):
                await pool.submit("error")
            with self.assertRaises(TimeoutError):
                await pool.submit("timeout")
            self.assertEqual(0, pool.completed)
            self.assertEqual(2, pool.failed)

    async def test_queue_bound_and_shutdown_cancel_pending_work(self):
        started = asyncio.Event()
        stopped = asyncio.Event()

        async def wait(_):
            started.set()
            try:
                await asyncio.Event().wait()
            finally:
                stopped.set()

        pool = AgentPool(wait, max_instances=1, max_queue_size=1)
        async with pool:
            active = asyncio.create_task(pool.submit(1))
            await asyncio.wait_for(started.wait(), 2)
            queued = asyncio.create_task(pool.submit(2))
            await asyncio.sleep(0)
            with self.assertRaises(asyncio.QueueFull):
                await pool.submit(3)
        outcomes = await asyncio.gather(active, queued, return_exceptions=True)
        self.assertTrue(all(isinstance(result, asyncio.CancelledError) for result in outcomes))
        self.assertTrue(stopped.is_set())
        self.assertEqual(0, pool.stats["pending"])
        self.assertEqual(0, pool.stats["busy"])

    async def test_caller_cancellation_releases_worker(self):
        started = asyncio.Event()
        stopped = asyncio.Event()

        async def work(payload):
            if payload == "blocked":
                started.set()
                try:
                    await asyncio.Event().wait()
                finally:
                    stopped.set()
            return payload

        async with AgentPool(work, max_instances=1) as pool:
            call = asyncio.create_task(pool.submit("blocked"))
            await asyncio.wait_for(started.wait(), 2)
            call.cancel()
            with self.assertRaises(asyncio.CancelledError):
                await call
            await asyncio.wait_for(stopped.wait(), 2)
            self.assertEqual("next", await asyncio.wait_for(pool.submit("next"), 2))

    def test_invalid_limits_are_rejected(self):
        for options in ({"min_instances": 0}, {"max_instances": 33},
                        {"max_queue_size": 0}, {"task_timeout": float("nan")},
                        {"idle_timeout": -1}, {"max_instances": True}):
            with self.assertRaises(ValueError):
                AgentPool(None, **options)


if __name__ == "__main__":
    unittest.main()
