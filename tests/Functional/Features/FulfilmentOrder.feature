Feature: Fulfilment Order Management
  In order to fulfil customer orders
  As a warehouse system
  I want to create, retrieve and delete fulfilment orders

  Background:
    Given a new fulfilment order ID is generated

  # ---------------------------------------------------------------------------
  # Creation
  # ---------------------------------------------------------------------------

  Scenario: Successfully create a single-line fulfilment order
    When I create a fulfilment order for store "STORE-01" with the following lines:
      | OrderLineNo | ArticleId | ExpectedQuantity | UnitOfMeasure | CustomerSupplyInstructions |
      | 1           | SKU-001   | 3.0              | Ea            |                            |
    Then the response status is 200 OK
    And the order can be retrieved with status "Pending"
    And the order line 1 has allocation status "Pending"

  Scenario: Successfully create a multi-line fulfilment order
    When I create a fulfilment order for store "STORE-01" with the following lines:
      | OrderLineNo | ArticleId | ExpectedQuantity | UnitOfMeasure | CustomerSupplyInstructions |
      | 1           | SKU-001   | 3.0              | Ea            |                            |
      | 2           | SKU-002   | 10.5             | Kg            | Handle with care           |
      | 3           | SKU-003   | 5.0              | Ea            |                            |
    Then the response status is 200 OK
    And the order can be retrieved with 3 order lines
    And all order lines have allocation status "Pending"

  Scenario: Customer supply instructions are persisted
    When I create a fulfilment order for store "STORE-01" with the following lines:
      | OrderLineNo | ArticleId | ExpectedQuantity | UnitOfMeasure | CustomerSupplyInstructions |
      | 1           | SKU-004   | 2.0              | Kg            | Fragile — store upright    |
    Then the response status is 200 OK
    And the order line 1 has customer supply instructions "Fragile — store upright"

  # ---------------------------------------------------------------------------
  # Retrieval
  # ---------------------------------------------------------------------------

  Scenario: Retrieve a created fulfilment order returns its fields
    Given a fulfilment order has been created for store "STORE-02" with order ID "ORD-ABC"
    When I retrieve the order by its ID
    Then the response status is 200 OK
    And the response body contains store "STORE-02"
    And the response body contains order ID "ORD-ABC"
    And the order version is 0

  # ---------------------------------------------------------------------------
  # Deletion
  # ---------------------------------------------------------------------------

  Scenario: Deleting an order removes it permanently
    Given a fulfilment order has been created for store "STORE-03" with order ID "ORD-DEL"
    When I delete the order by its ID
    Then the response status is 204 No Content
    And retrieving the order by its ID returns 404 Not Found
