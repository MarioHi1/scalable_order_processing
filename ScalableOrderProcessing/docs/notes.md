I would add "Queued" to the allowed statuses, because the process only really starts when a worker takes the order from the queue.
Right now started_at is set when the worker begins, and the status changes as soon as the queue picks up the order.

There is no type safety for order.status. I would use an enum here instead of a string.

There is no retry limit for orders. A failing order is reset to Open and tried again forever.

If an instance stops suddenly, its orders stay on InProgress forever. A cleanup job could reset old InProgress orders back to Open.

A possible extension: give each order a function id. Each function id runs a different function, so the worker can handle different kinds of jobs.
