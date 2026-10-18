# Legacy Office extraction fixtures

These small binary fixtures come from the Apache POI project and are used only by automated document extraction tests. They are not included in the extension package. Apache POI is a project of The Apache Software Foundation; the repository license is preserved in `APACHE-POI-LICENSE.txt`.

- `simple.doc`: https://github.com/apache/poi/blob/trunk/test-data/document/simple.doc
- `basic_test_ppt_file.ppt`: https://github.com/apache/poi/blob/trunk/test-data/slideshow/basic_test_ppt_file.ppt
- `Simple.xls`: https://github.com/apache/poi/blob/trunk/test-data/spreadsheet/Simple.xls

Downloaded 2026-10-18 from the corresponding `raw.githubusercontent.com/apache/poi/trunk/test-data/...` URLs. These source files remain unchanged during tests. PDF and modern ZIP document fixtures are generated from synthetic text inside the test output directory.
