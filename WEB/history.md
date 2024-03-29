# rpcdotnet-connector Version History
- 1.0.0: Initial release
- 1.0.1:
  - *fixed:* Missing `main` entry in `package.json`
- 1.0.2:
  - *fixed:* Missing `module` entry in `package.json`
- 1.0.3:
  - *fixed:* In case of remote exception `undefined` was thrown
- 1.0.4:
  - *fixed:* Parsing the response descriptor was case-sensitive
- 2.0.0:
  - *breaking:* Starting from this version, the library uses the *Fetch API* (instead of `XMLHttpRequest`), so the second (optional) parameter of `ApiConnectionFactory` takes the `fetch()` function.
  - *breaking:* Dropped setter support (since it can not be awaited). To invoke setters call their underlying functions (`set_xXx()`) directly.
  - *introduced:* Decorators
  - *introduced:* Layout validation
  - *introduced:* Schema API
  - *fixed:* Missing error message on timeout
- 2.0.1:
  - *fixed:* Incorrect binding
- 2.0.2:
  - *introduced:* In the schema, member aliases may be defined with a shorthand
- 3.0.0-preview1:
  - *done:* Logic has been rewritten from the scratch
  - *breaking:* Dropped `ApiConnectionFactory` class
  - *introduced:* `ApiConnection` class